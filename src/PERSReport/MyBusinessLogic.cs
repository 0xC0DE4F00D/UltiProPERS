using CommandLine;
using PERSReport.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PERSReport.Utilities;
using PERSReport.Models;
using System.ComponentModel;

namespace PERSReport
{
    class MyBusinessLogic : IIntegrationService
    {
        private ExceptionLog _Log;
        private string _TimeTag;
        private CognosReportAsAService _RaaS;

        private ExcelIntegration _ExcelApp;
        private PersConfiguration _PersConfiguration;

        // File paths and names for input reports.
        private string _ByControlDateReportPath;         // Path to input file "RunPERSbyPeriodControlDate" report.
        private string _ByDailyHoursReportPath;          // Path to input file "RunPERSbyDailyHours" report
        private string _WorksheetName;                   // The worksheet name for each report - Cognos default is 'page'.

        private string _OutputFolder;                    // Path to exported Ultipro files
        public char _FieldSeparator;                     // Field separation character.
        public bool _CreateCorrectionReport;             // Produce MRL Correction records instead of Regular record types.

        private DataSet _InputRecords;                   // Container for Input rows & columns tables.
        private DataSet _OutputRecords;                  // Container for Ouput rows & columns tables.

        // Reporting period details provided from the application command line.
        private string _StatedPeriodControlDay;          // PeriodControlDate (i.e. the date of payroll processing closure)
        private DateTime _StatedPeriodControlDate;       // Use this to format the _PeriodControlDay for Cognos and Calendar inquiry.

        // Reporting period details derived from the Payroll Calendar configuration.
        // The default settings are imported from file PersConfiguration.json, but may be overridden
        // via command line options.
        private string _MonthlyReportNumber;             // From a DRS perspective, this is report number X of Y
        private Int32 _nMonthlyReportNumber;
        private string _ExpectedNumberOfMonthlyReports;  // From a DRS perspective, there are Y expected reports this month
        private Int32 _nExpectedNumberOfMonthlyReports;

        // Force creation of a single-period report event if a split-period is recommended by the Calendar.
        private bool _ForceCreateSinglePeriodReport;

        public MyBusinessLogic()
        {
            _Log = null;
            _TimeTag = string.Empty;
            _RaaS = null;
            _InputRecords = null;
            _OutputRecords = null;
            _ExcelApp = null;
            _PersConfiguration = null;
            _ForceCreateSinglePeriodReport = false;
            _nMonthlyReportNumber = 0;
            _nExpectedNumberOfMonthlyReports = 0;
        }

        public MyBusinessLogic(ref PersConfiguration persConfiguration, ref CognosReportAsAService raas, ref ExcelIntegration excelApp, ref ExceptionLog log, string timeTag)
        {
            //Debug.Assert(raas is CognosReportAsAService);
            Debug.Assert(excelApp is ExcelIntegration);
            Debug.Assert(log is ExceptionLog);
            Debug.Assert(string.IsNullOrEmpty(timeTag) == false);
            _RaaS = raas;
            _ExcelApp = excelApp;
            _PersConfiguration = persConfiguration;
            _Log = log;
            _TimeTag = timeTag;
            _InputRecords = new DataSet("PersInputs");
            _OutputRecords = new DataSet("PersOutputs");
            _ForceCreateSinglePeriodReport = false;
            _nMonthlyReportNumber = 0;
            _nExpectedNumberOfMonthlyReports = 0;
        }

        public bool ParseArguments(string[] args)
        {
            Debug.Assert(args != null);

            Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(o =>
            {
                if (!string.IsNullOrEmpty(o.ByPeriodReport))
                    _ByControlDateReportPath = Path.GetFullPath(o.ByPeriodReport);

                if (!string.IsNullOrEmpty(o.ByDailyHoursReport))
                    _ByDailyHoursReportPath = Path.GetFullPath(o.ByDailyHoursReport);

                if (!string.IsNullOrEmpty(o.WorksheetName))
                    _WorksheetName = o.WorksheetName;

                if (!string.IsNullOrEmpty(o.OutputFolder))
                    _OutputFolder = Path.GetFullPath(o.OutputFolder);

                if (!string.IsNullOrEmpty(o.PeriodControlDate))
                {
                    _StatedPeriodControlDay = o.PeriodControlDate;
                }
                else
                {
                    _StatedPeriodControlDate = DateTime.Today;
                }

                if (!string.IsNullOrEmpty(o.MonthlyReportNumber))
                    _MonthlyReportNumber = o.MonthlyReportNumber;

                if (!string.IsNullOrEmpty(o.ExpectedNumberOfMonthlyReports))
                    _ExpectedNumberOfMonthlyReports = o.ExpectedNumberOfMonthlyReports;

                _FieldSeparator = o.FieldSeparator;

                _CreateCorrectionReport = o.CreateCorrectionReport;

                _ForceCreateSinglePeriodReport = o.ForceCreateSinglePeriodReport;
            })
            .WithNotParsed<Options>((errs) => HandleParseErrors(errs));
            return true;
        }


        /// <summary>
        /// **** Run - Execute application business logic.
        /// </summary>
        /// <returns></returns>
        public bool Run()
        {
            Debug.Assert(_InputRecords != null);
            Debug.Assert(_OutputFolder != null);

            try
            {
                if (ValidateInputArguments() == false)
                    return false;

                //
                // Establish reporting period dates and rates.
                //

                PayPeriod payPeriod;

                if (_PersConfiguration.SelectPayPeriodRecord(_StatedPeriodControlDate, out payPeriod) == false)
                {
                    throw (new Exception($"Failed to select PayPeriod record from imported PERS configuration."));
                }

                G.Display($"PERS for Check Date = {payPeriod.CheckDate.ToShortDateString()}");
                G.DisplayNotification($"\tPeriodControlDate={_PersConfiguration.PeriodControlDate().ToShortDateString()}");
                G.DisplayNotification($"\tStartDate={_PersConfiguration.PayPeriodStartDate().ToShortDateString()}");
                G.DisplayNotification($"\tEndDate={_PersConfiguration.PayPeriodEndDate().ToShortDateString()}");
                G.DisplayNotification($"\tReportPeriod={_PersConfiguration.ReportPeriod()}");
                G.DisplayNotification($"\tMonthlyReportNumber={_PersConfiguration.MonthlyReportNumber()}");
                G.DisplayNotification($"\tExpectedNumberOfMonthlyReports={_PersConfiguration.ExpectedNumberOfMonthlyReports()}");
                G.DisplayNotification($"\tEarningPeriod={_PersConfiguration.EarningPeriod()}");
                if (_PersConfiguration.IsThisASplitMonthPayPeriod() == true)
                {
                    G.DisplayNotification($"\tSplit Month PayPeriod:");
                    G.DisplayNotification($"\t\tPriorMonthEarningPeriod={_PersConfiguration.PriorEarningPeriod()}");
                    int workDaysInMonth1, workDaysInMonth2;
                    if (_PersConfiguration.GetWorkWeekDaysInSelectedPayPeriodByMonth(out workDaysInMonth1, out workDaysInMonth2) == true)
                    {
                        G.DisplayNotification($"\t\tWorkWeekDaysInPriorMonth={workDaysInMonth1}");
                        G.DisplayNotification($"\t\tWorkWeekDaysInReportMonth={workDaysInMonth2}");
                    }
                }
                if (_PersConfiguration.IsSplitPeriodWithRateChanges() == true)
                {
                    G.DisplayNotification($"\tRate Changes within PayPeriod:\t");
                    G.DisplayNotification($"\t\t{_PersConfiguration.PriorEarningPeriod()} Member Rates:");
                    G.DisplayNotification($"\t\t\tPERS1={G.R4(_PersConfiguration.Month1MemberContributionRate("PERS1"))}");
                    G.DisplayNotification($"\t\t\tPERS2={G.R4(_PersConfiguration.Month1MemberContributionRate("PERS2"))}");
                    G.DisplayNotification($"\t\t{_PersConfiguration.EarningPeriod()} Member Rates:");
                    G.DisplayNotification($"\t\t\tPERS1={G.R4(_PersConfiguration.Month2MemberContributionRate("PERS1"))}");
                    G.DisplayNotification($"\t\t\tPERS2={G.R4(_PersConfiguration.Month2MemberContributionRate("PERS2"))}");
                    G.DisplayNotification($"\t\t{_PersConfiguration.PriorEarningPeriod()} Company Rates:");
                    G.DisplayNotification($"\t\t\tPERS1={G.R4(_PersConfiguration.Month1EmployerContributionRate("PERS1"))}");
                    G.DisplayNotification($"\t\t\tPERS2={G.R4(_PersConfiguration.Month1EmployerContributionRate("PERS2"))}");
                    G.DisplayNotification($"\t\t\tPERS3={G.R4(_PersConfiguration.Month1EmployerContributionRate("P3"))}");
                    G.DisplayNotification($"\t\t{_PersConfiguration.EarningPeriod()} Company Rates:");
                    G.DisplayNotification($"\t\t\tPERS1={G.R4(_PersConfiguration.Month2EmployerContributionRate("PERS1"))}");
                    G.DisplayNotification($"\t\t\tPERS2={G.R4(_PersConfiguration.Month2EmployerContributionRate("PERS2"))}");
                    G.DisplayNotification($"\t\t\tPERS3={G.R4(_PersConfiguration.Month2EmployerContributionRate("P3"))}\n");
                }


                //
                // Helper classes that facilitate import and parsing of source-data cognos reports.
                //

                PersByPeriodControlDate persByPeriodControlDate = new PersByPeriodControlDate(ref _InputRecords, ref _Log, ref _PersConfiguration, _TimeTag);
                PersByChargeDates persByChargeDates = new PersByChargeDates(ref _InputRecords, ref _Log, ref _PersConfiguration, _TimeTag);

                //
                // **** SOURCE DATA RETRIEVAL STAGE ****
                //

                //
                // **** Either download the Cognos reports via ReportAsAService API or import manually exported report excel files.
                //

                if ((_RaaS is CognosReportAsAService) && (_RaaS != null))
                {
                    //
                    // Option #1.  Download from Web Services.  Establish CSV output filenames for the reports exported from Cognos via RaaS API.
                    // Note that the RaaS API is used to download reports as CSV files, which are then imported here.
                    //

                    _ByControlDateReportPath = _OutputFolder + "\\" + "FromCognos" + "\\" + _PersConfiguration.PeriodControlDay() + "-PersByPeriod-" + _TimeTag + ".csv";
                    _ByDailyHoursReportPath = _OutputFolder + "\\" + "FromCognos" + "\\" + _PersConfiguration.PeriodControlDay() + "-ChargeByHours-" + _TimeTag + ".csv";

                    G.Display($"Downloading Cognos reports...");

                    //
                    // Download the PERS by PeriodControlDate dataset via the RunPERSbyPeriodControlDateV2 Cognos report.
                    //

                    G.DisplayNotification($"\tPeriod Control Date report. {persByPeriodControlDate.CognosReportID}");

                    if (_RaaS.GetPersByPeriodControlDate(persByPeriodControlDate.CognosReportID, _PersConfiguration.PeriodControlDate().ToShortDateString(), _ByControlDateReportPath) == false)
                        throw (new Exception($"Failed to GetReport from Cognos Web Services. ReportID={persByPeriodControlDate.CognosReportID}"));

                    //
                    // Download the PERS by ChargeDate dataset via the RunPERSbyDailyHours Cognos report.
                    //

                    G.DisplayNotification($"\tCharge Dates report. {persByChargeDates.CognosReportID}");

                    if (_RaaS.GetPersByDailyHours(persByChargeDates.CognosReportID, _PersConfiguration.PeriodControlDate().ToShortDateString(), _ByDailyHoursReportPath) == false)
                        throw (new Exception($"Failed to GetReport from Cognos Web Services. ReportID={persByChargeDates.CognosReportID}"));

                    //
                    // Import the newly downloaded report CSV files.
                    //

                    G.Display($"\nParsing downloaded reports...");

                    if (persByPeriodControlDate.ParseCsvReportFile(ref _InputRecords, _ByControlDateReportPath) == false)
                        throw (new Exception($"Failed to import Cognos file. File={_ByControlDateReportPath}"));

                    if (persByChargeDates.ParseCsvReportFile(ref _InputRecords, _ByDailyHoursReportPath) == false)
                        throw (new Exception($"Failed to import Cognos file. File={_ByDailyHoursReportPath}"));
                }
                else 
                {
                    //
                    // **** Option #2, import from previously exported report excel files
                    //

                    G.Display($"\nImporting previously downloaded Cognos report files...");

                    Debug.WriteLine($"\nREPORTS: {_ByControlDateReportPath}\n{_ByDailyHoursReportPath}\n");

                    if (_ExcelApp == null)
                        throw (new Exception($"Excel integration is not properly configured."));

                    if ((string.IsNullOrEmpty(_ByControlDateReportPath)) || 
                        (string.IsNullOrEmpty(_ByDailyHoursReportPath)))
                        throw (new Exception($"Missing Excel input file path.  See program usage guidance"));

                    //
                    // Import the PERSbyPeriodControlDate source dataset via the RunPERSbyPeriodControlDateV2 Cognos excel report.
                    //

                    G.DisplayNotification($"\tPeriod Control Date report. file={_ByControlDateReportPath}");

                    if (persByPeriodControlDate.ParseExcelReportFile(ref _InputRecords, ref _ExcelApp, _ByControlDateReportPath, _WorksheetName) == false)
                        throw (new Exception($"Failed to import Cognos file. File={_ByControlDateReportPath}"));

                    //
                    // Import the PERSbyDailyHours source dataset via the RunPERSbyDailyHours Cognos excel report.
                    //

                    G.DisplayNotification($"\tCharge Dates report. file={_ByDailyHoursReportPath}");

                    if (persByChargeDates.ParseExcelReportFile(ref _InputRecords, ref _ExcelApp, _ByDailyHoursReportPath, _WorksheetName) == false)
                        throw (new Exception($"Failed to import Cognos file. File={_ByDailyHoursReportPath}"));
                }

                //
                // **** RECORDS PROCESSING STAGE ****
                //

                G.Display($"\nProcessing ChargeDate (CD) and PeriodControlDate (PC) records...");

                PersRecordParser persParser = new PersRecordParser(ref _PersConfiguration, 
                                                                   ref persByChargeDates,
                                                                   ref persByPeriodControlDate,
                                                                   ref _Log, _TimeTag);

                //
                // **** Process the Report Data for DRS MRL file-format export... Parse records to corresponding DBR and DCR record types.
                //

                if (persParser.ProcessInputRecords(ref _InputRecords, ref _OutputRecords) == false)
                    throw (new Exception("Error parsing contribution records."));

                //
                // **** OUTPUT FORMATTING STAGE ****
                //

                //
                // Format the various MRL report segments by record type (SUM, MBR, DBR, DCR)
                //

                G.Display($"\nFormatting output records and files...");

                PersReportFormatter formatter = new PersReportFormatter(ref _Log, _TimeTag);

                if (formatter.FormatFinalReportLineItems(ref _OutputRecords, _PersConfiguration.ReportPeriod(), _PersConfiguration.MonthlyReportNumber("G"), _CreateCorrectionReport) == false)
                    throw (new Exception("Error formatting DRS report file."));

                string persReportFile = _OutputFolder + "\\" + _PersConfiguration.PeriodControlDay() + "-Send2DRS-" + _TimeTag + ".txt";

                if (formatter.WritePersReportFile(persReportFile) == false)
                    throw (new Exception($"Error exporting DRS report file. File={persReportFile}"));

                //
                // Export a more readable report version within Excel.  Use this for DRS report validation.
                //

                string persValidationFile = _OutputFolder + "\\" + _PersConfiguration.PeriodControlDay() + "-Validate-" + _TimeTag + ".xlsx";

                // Export _OuputRecords data-set to Excel as individual worksheets.
                if (formatter.WritePersValidationFile(ref _OutputRecords, ref _ExcelApp, persValidationFile) == false)
                    throw (new Exception($"Error exporting PERS validation file. File={persValidationFile}"));

                G.Display($"\nSee DRS report file: \n\t{persReportFile}");
                G.Display($"See GCPUD validation file: \n\t{persValidationFile}\n");
            }
            catch (Exception ex)
            {
                _Log.LogException("RunPERS::Run", ex.ToString(), "EXCEPTION");
                G.DisplayError(ex.Message);
                return false;
            }
            finally
            {
                if (_InputRecords != null)
                    _InputRecords.Clear();
                if (_OutputRecords != null)
                    _OutputRecords.Clear();
            }
            return true;
        }


        private static void HandleParseErrors(IEnumerable<Error> errs)
        {
            throw (new Exception(string.Format("Error parsing command line options.")));
        }


        private bool ValidateInputArguments()
        {

            try
            {
                // - PeriodControlDay YYYYMMDD
                if (string.IsNullOrEmpty(_StatedPeriodControlDay) == false)
                {
                    // Derive from command line provided _StatedPeriodControlDay.
                    _StatedPeriodControlDate = new DateTime(int.Parse(_StatedPeriodControlDay.Substring(0, 4)),
                                                            int.Parse(_StatedPeriodControlDay.Substring(4, 2)),
                                                            int.Parse(_StatedPeriodControlDay.Substring(6, 2)));
                    if (!((_StatedPeriodControlDate.Year >= 2020) &&
                        ((_StatedPeriodControlDate.Month >= 1) && (_StatedPeriodControlDate.Month <= 12)) &&
                        ((_StatedPeriodControlDate.Day >= 1) && (_StatedPeriodControlDate.Day <= 31))))
                        throw (new Exception($"Invalid input parameter, PeriodControlDate={_StatedPeriodControlDay}"));
                }
            }
            catch(System.ArgumentOutOfRangeException)
            {
                G.DisplayError($"Invalid Period Control Date input format. Use numerical format YYYYMMDD");
                return false;
            }

            if (string.IsNullOrEmpty(_MonthlyReportNumber) == false)
            {
                _nMonthlyReportNumber = int.Parse(_MonthlyReportNumber);
                if (!((_nMonthlyReportNumber >= 1) && (_nMonthlyReportNumber <= 3)))
                    throw (new Exception("Invalid input parameter, ReportNumber."));
                _PersConfiguration.OverrideConfiguredMonthlyReportNumber(_nMonthlyReportNumber);
            }

            if (string.IsNullOrEmpty(_ExpectedNumberOfMonthlyReports) == false)
            {
                _nExpectedNumberOfMonthlyReports = int.Parse(_ExpectedNumberOfMonthlyReports);
                if (!((_nExpectedNumberOfMonthlyReports >= 2) && (_nExpectedNumberOfMonthlyReports <= 3)))
                    throw (new Exception("Invalid input parameter, ExpectedNumberOfReportNumber."));
                _PersConfiguration.OverrideConfiguredExpectedNumberOfMonthlyReports(_nExpectedNumberOfMonthlyReports);
            }

            // - Report File and Folder
            if (string.IsNullOrEmpty(_OutputFolder))
                throw (new Exception($"Invalid input parameter, OutputFolder={_OutputFolder}"));

            return true;
        }

    }

}
