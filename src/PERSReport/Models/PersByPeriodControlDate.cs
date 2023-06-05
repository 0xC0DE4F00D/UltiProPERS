using PERSReport.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using PERSReport.Utilities;
using Microsoft.Office.Interop.Excel;
using System.Data;
using System.Diagnostics;
using GenericParsing;
using System.Data.Odbc;

namespace PERSReport.Models
{
    class PersByPeriodControlDate
    {
        ExceptionLog _Log;
        string _TimeTag;
        PersConfiguration _PersConfiguration;

        public const string TableName = @"PayPeriodByEmployee";

        public string CognosReportID { get; }

        public PersByPeriodControlDate()
        {
            _Log = null;
            _TimeTag = string.Empty;
            _PersConfiguration = null;

            CognosReportID = PERSReport.Properties.Settings.Default.RunPERSbyPeriodControlDateV3ID;
        }

        ~PersByPeriodControlDate()
        {
            _Log = null;
            _TimeTag = string.Empty;
            _PersConfiguration = null;
        }

        public PersByPeriodControlDate(ref DataSet inputRecords, ref ExceptionLog log, ref PersConfiguration persConfiguration, string timeTag)
        {
            _Log = log;
            _TimeTag = timeTag;
            _PersConfiguration = persConfiguration;

            CognosReportID = PERSReport.Properties.Settings.Default.RunPERSbyPeriodControlDateV3ID;
        }


        public bool ParseExcelReportFile(ref DataSet inputRecords, ref ExcelIntegration excel, string filePath, string workSheetName=@"page")
        {
            Debug.Assert(inputRecords != null);
            Debug.Assert(excel != null);

            Debug.WriteLine($"Parsing excel report and worksheet: {filePath} : {workSheetName}");

            if ((excel == null) || (inputRecords == null))
                throw new Exception("null argument");

            if ((string.IsNullOrEmpty(filePath)) || (string.IsNullOrEmpty(workSheetName)))
                throw new Exception("Please specify a valid data-file path and worksheet name");

            if ((inputRecords.Tables.Contains(PersByPeriodControlDate.TableName)) && (inputRecords.Tables[PersByPeriodControlDate.TableName].Rows.Count > 0))
                inputRecords.Tables[PersByPeriodControlDate.TableName].Clear();

            if (excel.GetWorksheetFromExcel(filePath, workSheetName, ref inputRecords, PersByPeriodControlDate.TableName) == false)
                throw new Exception($"No records available within the specified file, {filePath}, or worksheet, {workSheetName}.\n");

            if (!(inputRecords.Tables[PersByPeriodControlDate.TableName].Rows.Count > 0))
                throw new Exception($"No records imported from Cognos excel report file {filePath}.");

            return true;
        }


        public bool ParseCsvReportFile(ref DataSet inputRecords, string filePath, char fieldSeparator=',')
        {
            Debug.Assert(inputRecords != null);

            Debug.WriteLine($"Parsing CSV report file: {filePath}");

            if (string.IsNullOrEmpty(filePath))
                throw new Exception("Please specify a valid data-file path.");

            if (inputRecords == null)
                throw new Exception("null argument");

            if ((inputRecords.Tables.Contains(PersByPeriodControlDate.TableName)) && (inputRecords.Tables[PersByPeriodControlDate.TableName].Rows.Count > 0))
                inputRecords.Tables[PersByPeriodControlDate.TableName].Clear();

            DataSet ds = null;
            using (GenericParserAdapter parser = new GenericParserAdapter())
            {
                parser.SetDataSource(filePath);
                parser.ColumnDelimiter = fieldSeparator;
                parser.FirstRowHasHeader = true;
                ds = parser.GetDataSet();
                if ((ds != null) && (ds.Tables[0].Rows.Count > 0))
                {
                    ds.Tables[0].TableName = PersByPeriodControlDate.TableName;
                    inputRecords.Tables.Add(ds.Tables[0].Copy());
                    ds.Clear();
                }
            }

            if (!(inputRecords.Tables[PersByPeriodControlDate.TableName].Rows.Count > 0))
                throw new Exception($"No records imported from Cognos CSV report file {filePath}.");

            return true;
        }


        static Dictionary<string, string> _Pers3InvestmentPrograms = new Dictionary<string, string>
        {
            { "P3AS", "SELF" },
            { "P3AW", "WSIB" },
            { "P3BS1", "SELF" },
            { "P3BS2", "SELF" },
            { "P3BS3", "SELF" },
            { "P3BW1", "WSIB" },
            { "P3BW2", "WSIB" },
            { "P3BW3", "WSIB" },
            { "P3CS1", "SELF" },
            { "P3CS2", "SELF" },
            { "P3CS3", "SELF" },
            { "P3CW1", "WSIB" },
            { "P3CW2", "WSIB" },
            { "P3CW3", "WSIB" },
            { "P3DS", "SELF" },
            { "P3DW", "WSIB" },
            { "P3ES", "SELF" },
            { "P3EW", "WSIB" },
            { "P3FS", "SELF" },
            { "P3FW", "WSIB" },
            { "P3NH", "SELF" }   // Note: P3NH is a transferring Plan 3 new hire indicator. Employee has 90 days to select investment program (i.e. any other code).
        };

        // TODO : Validate that we have all codes (e.g. see the Configuration Validation Report)

        static Dictionary<string, string> _Pers3RateOptions = new Dictionary<string, string>
        {
            { "P3AS", "A" },
            { "P3AW", "A" },
            { "P3BS1", "B" },
            { "P3BS2", "B" },
            { "P3BS3", "B" },
            { "P3BW1", "B" },
            { "P3BW2", "B" },
            { "P3BW3", "B" },
            { "P3CS1", "C" },
            { "P3CS2", "C" },
            { "P3CS3", "C" },
            { "P3CW1", "C" },
            { "P3CW2", "C" },
            { "P3CW3", "C" },
            { "P3DS", "D" },
            { "P3DW", "D" },
            { "P3ES", "E" },
            { "P3EW", "E" },
            { "P3FS", "F" },
            { "P3FW", "F" },
            { "P3NH", "A" }    // Note: P3NH is a transferring Plan 3 new hire indicator. Employee has 90 days to select investment program (i.e. any other code).
        };


        /// <summary>
        /// ParseContributionRecords : Aggregate output records by member classification type.
        /// </summary>
        /// <param name="inputRecords"></param>
        /// <param name="outputRecords"></param>
        /// <param name="splitMonth"></param>
        /// <returns></returns>
        public bool ParseContributionRecords(ref DataSet inputRecords, ref DataSet outputRecords, bool splitMonth=false)
        {
            Debug.Assert(inputRecords != null);
            Debug.Assert(outputRecords != null);

            if ((inputRecords == null) ||
                (outputRecords == null) ||
                (inputRecords.Tables.Contains(PersByPeriodControlDate.TableName) == false))
                throw new Exception($"PersRecordParser::ParseContributionRecords : null input arguments.");

            // Parse and transform PERS1, PERS2, PERS3, DBR and DCR records.
            foreach (DataRow pers in inputRecords.Tables[PersByPeriodControlDate.TableName].AsEnumerable())
            {
                string dbc = G.Field(pers, "DeductionBenefitCode").Trim();
                string employeeStatus = G.Field(pers, "EmployeeStatusCode").Trim();
                string checkAddMode = G.Field(pers, "CheckAddMode").Trim();

                // Log any CheckAddMode Type record that is not "R" (regular).  Examples include "C" and "M".  This record type should be manually verified.
                if (G.IsDiff(checkAddMode, "R"))
                    G.AddNoteToDataRow(pers, @"Non-Regular Check Mode");

                // Log any employee when Terminated.  
                if (G.IsSame(employeeStatus, "T"))
                    G.AddNoteToDataRow(pers, @"Termination");

                // Aggregate records based upon DeductionBenefitCode (e.g. PERS2, PERS3, PERS1, PERS0)
                if (G.IsSame(dbc, "PERS2"))
                {
                    AddPersDbrRecord(ref outputRecords, pers, "2", splitMonth);
                }
                else if (G.Contains(dbc, "P3"))
                {
                    AddPers3Record(ref outputRecords, pers, splitMonth);
                }
                else if (G.IsSame(dbc, "PERS1"))
                {
                    AddPersDbrRecord(ref outputRecords, pers, "1", splitMonth);
                }
                else if (G.IsSame(dbc, "PERS0"))   // PERS retirees who are still working.
                {
                    AddPersDbrRecord(ref outputRecords, pers, "0", splitMonth);
                }
                else  // needs manual review?
                {
                    G.AddNoteToDataRow(pers, @"Invalid PERS classification code");
                }

                // Add a Manual Validation record if there are notes attached to this employee pers record.
                if (string.IsNullOrEmpty(G.Field(pers, "Note")) == false)
                    AddManualVerifyRecord(ref outputRecords, pers, null, splitMonth);
            }
            return true;
        }


        private bool AddPersDbrRecord(ref DataSet outputRecords, DataRow pers, string planCode, bool splitMonth=false)
        {
            // Use the function for only planCodes of "0", "1", or "2".  Use the AddPers3Record for all planCode "3" records.

            string employeeNumber = G.Field(pers, "EmployeeNumber");
            string persClass = G.Field(pers, "PersClassification").Trim();
            string lastName = G.Field(pers, "LastName");
            string ssn = G.Field(pers, "SSN");

            if (splitMonth == true)
            {
                decimal split1Hours = G.Decimal(pers, "Split1Hours", true);
                decimal split1Pay = G.Decimal(pers, "Split1Pay", true);
                decimal split2Hours = G.Decimal(pers, "Split2Hours", true);
                decimal split2Pay = G.Decimal(pers, "Split2Pay", true);
                decimal split1CompanyContribution = G.Decimal(pers, "Split1CompanyContribution", true);
                decimal split1EmployeeContribution = G.Decimal(pers, "Split1EmployeeContribution", true);
                decimal split2CompanyContribution = G.Decimal(pers, "Split2CompanyContribution", true);
                decimal split2EmployeeContribution = G.Decimal(pers, "Split2EmployeeContribution", true);

                // Only include the Split1 record if it includes non-zero data across the board (e.g. new employees may have no Split1 contributions).
                if ((split1Hours != 0.0m) || (split1Pay != 0.0m) || (split1CompanyContribution != 0.0m) || (split1EmployeeContribution != 0.0m))
                { 
                    // SplitMonth1
                    outputRecords.Tables["DefinedBenefitRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                            G.Field(pers, "ReportNumber"),
                                                                            employeeNumber,
                                                                            lastName,
                                                                            ssn,
                                                                            planCode,                                // PlanCode "0", "1", "2"
                                                                            persClass,                               // TypeCode
                                                                            G.Field(pers, "Split1EarningPeriod"),
                                                                            split1Hours,                             // Hours
                                                                            split1Pay,                               // Compensation - DeductionCalcBasisAmount, not TotalEarningAmount 
                                                                            split1CompanyContribution,               // Benefit
                                                                            split1EmployeeContribution,              // DeferredContribution
                                                                            "A");                                    // Status
                }
                if ((split2Hours != 0.0m) || (split2Pay != 0.0m) || (split2CompanyContribution != 0.0m) || (split2EmployeeContribution != 0.0m))
                {
                    // SplitMonth2
                    outputRecords.Tables["DefinedBenefitRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                       G.Field(pers, "ReportNumber"),
                                                                       employeeNumber,
                                                                       lastName,
                                                                       ssn,
                                                                       planCode,                                  // PlanCode "0", "1", "2"
                                                                       persClass,                                 // TypeCode
                                                                       G.Field(pers, "Split2EarningPeriod"),
                                                                       split2Hours,                               // Hours
                                                                       split2Pay,                                 // Compensation - DeductionCalcBasisAmount, not TotalEarningAmount 
                                                                       split2CompanyContribution,                 // Benefit
                                                                       split2EmployeeContribution,                // DeferredContribution
                                                                       "A");
                }
            }
            else
            {
                outputRecords.Tables["DefinedBenefitRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                       G.Field(pers, "ReportNumber"),
                                                                       employeeNumber,
                                                                       lastName,
                                                                       ssn,
                                                                       planCode,                                    // PlanCode "0", "1", "2"
                                                                       persClass,                                   // TypeCode
                                                                       G.Field(pers, "EarningPeriod"),
                                                                       G.Decimal(pers, "ChargeDateTotalHours", true),     // Hours
                                                                       G.Decimal(pers, "DeductionCalcBasisAmount", true), // Compensation - if retiree use TotalEarningAmount 
                                                                       G.Decimal(pers, "CurrentAmountEmployer", true),    // Benefit
                                                                       G.Decimal(pers, "CurrentAmountEmployee", true),    // DeferredContribution
                                                                       "A");                                        // Status
            }
            return true;
        }


        private bool AddPers3Record(ref DataSet outputRecords, DataRow pers, bool splitMonth = false)
        {
            // PERS3
            string employeeNumber = G.Field(pers, "EmployeeNumber");
            string persClass = G.Field(pers, "PersClassification").Trim();
            string lastName = G.Field(pers, "LastName");
            string ssn = G.Field(pers, "SSN");
            string dbc = G.Field(pers, "DeductionBenefitCode").Trim();
            string investProgram;
            string rateOption;

            // For Plan3 members, also create a DBR type record with summary totals for TotalEarningAmount and CurrentAmountEmployer.

            if (splitMonth == true)
            {
                decimal split1Hours = G.Decimal(pers, "Split1Hours", true);
                decimal split1Pay = G.Decimal(pers, "Split1Pay", true);
                decimal split1CompanyContribution = G.Decimal(pers, "Split1CompanyContribution", true);
                decimal split2Hours = G.Decimal(pers, "Split2Hours", true);
                decimal split2Pay = G.Decimal(pers, "Split2Pay", true);
                decimal split2CompanyContribution = G.Decimal(pers, "Split2CompanyContribution", true);

                // Only include the Split1 record if it includes non-zero data (e.g. new employees may have no Split1 contributions).
                if ((split1Hours != 0.0m) || (split1Pay != 0.0m) || (split1CompanyContribution != 0.0m))
                {
                    // Split1Month
                    outputRecords.Tables["DefinedBenefitRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                       G.Field(pers, "ReportNumber"),
                                                                       employeeNumber,
                                                                       lastName,
                                                                       ssn,
                                                                       "3",                                      // PlanCode
                                                                       persClass,                                // TypeCode
                                                                       G.Field(pers, "Split1EarningPeriod"),
                                                                       split1Hours,                             // Hours
                                                                       split1Pay,                               // Compensation - DeductionCalcBasisAmount, not TotalEarningAmount 
                                                                       split1CompanyContribution,               // Benefit
                                                                       0.00m,                                   // DeferredContribution specified within corresponding DCR record below...
                                                                       "A");                                    // Status
                }

                if ((split2Hours != 0.00m) || (split2Pay != 0.0m) || (split2CompanyContribution != 0.0m))
                {
                    // Split2Month
                    outputRecords.Tables["DefinedBenefitRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                       G.Field(pers, "ReportNumber"),
                                                                       employeeNumber,
                                                                       lastName,
                                                                       ssn,
                                                                       "3",                                             // PlanCode
                                                                       persClass,                                       // TypeCode
                                                                       G.Field(pers, "Split2EarningPeriod"),
                                                                       split2Hours,                                     // Hours
                                                                       split2Pay,                                       // Compensation - DeductionCalcBasisAmount, not TotalEarningAmount
                                                                       split2CompanyContribution,                       // Benefit
                                                                        0.00m,                                          // DeferredContribution specified within corresponding DCR record below...
                                                                       "A");                                            // Status
                } 
                
                // Add Split-Month DCR records.
                _Pers3InvestmentPrograms.TryGetValue(dbc, out investProgram);
                _Pers3RateOptions.TryGetValue(dbc, out rateOption);
                decimal split1EmployeeContribution = G.Decimal(pers, "Split1EmployeeContribution", true);
                decimal split2EmployeeContribution = G.Decimal(pers, "Split2EmployeeContribution", true);

                // Only include the Split1 record if it includes non-zero employee contribution (e.g. new employees may have no Split1 contributions).
                if ((split1EmployeeContribution != 0.0m) && (string.IsNullOrEmpty(investProgram) == false))
                {
                    // Split1Month DCR
                    outputRecords.Tables["DefinedContributionRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                            G.Field(pers, "ReportNumber"),
                                                                            employeeNumber,
                                                                            lastName,
                                                                            ssn,
                                                                            G.Field(pers, "DeductionBenefitCode"),          // PlanCode
                                                                            split1EmployeeContribution,                     // DeferredContribution 
                                                                            investProgram,                                  // InvestProgram
                                                                            rateOption);                                    // RateOption
                }

                // Only include the Split2 record if it includes non-zero employee (e.g. new employees may have no Split2 contributions nor investProgram selections).
                if ((split2EmployeeContribution != 0.0m) && (string.IsNullOrEmpty(investProgram) == false))
                {
                    // Split2Month DCR
                    outputRecords.Tables["DefinedContributionRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                            G.Field(pers, "ReportNumber"),
                                                                            employeeNumber,
                                                                            lastName,
                                                                            ssn,
                                                                            G.Field(pers, "DeductionBenefitCode"),    // PlanCode
                                                                            split2EmployeeContribution,               // DeferredContribution 
                                                                            investProgram,                            // InvestProgram
                                                                            rateOption);                              // RateOption
                }
            }
            else  // Contiguous period reporting - not split
            {
                decimal employeeContribution = G.Decimal(pers, "CurrentAmountEmployee", true);
                _Pers3InvestmentPrograms.TryGetValue(dbc, out investProgram);
                _Pers3RateOptions.TryGetValue(dbc, out rateOption);

                // Contiguous pay-period records
                outputRecords.Tables["DefinedBenefitRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                       G.Field(pers, "ReportNumber"),
                                                                       employeeNumber,
                                                                       lastName,
                                                                       ssn,
                                                                       "3",                                                // PlanCode
                                                                       persClass,                                          // TypeCode
                                                                       G.Field(pers, "EarningPeriod"),
                                                                       G.Decimal(pers, "ChargeDateTotalHours", true),      // Hours
                                                                       G.Decimal(pers, "DeductionCalcBasisAmount", true),  // Compensation - not TotalEarningAmount
                                                                       G.Decimal(pers, "CurrentAmountEmployer", true),     // Benefit
                                                                       0.00m,                                              // DeferredContribution specified within corresponding DCR record below...
                                                                       "A");                                               // Status

                if ((employeeContribution != 0.0m) && (string.IsNullOrEmpty(investProgram) == false))
                {
                    // Add a DCR record.
                    outputRecords.Tables["DefinedContributionRecords"].Rows.Add(G.Field(pers, "ReportPeriod"),
                                                                                G.Field(pers, "ReportNumber"),
                                                                                employeeNumber,
                                                                                lastName,
                                                                                ssn,
                                                                                G.Field(pers, "DeductionBenefitCode"),      // PlanCode
                                                                                employeeContribution,                       // DeferredContribution 
                                                                                investProgram,                              // InvestProgram
                                                                                rateOption);                                // RateOption
                }
            }

            return true;
        }


        private bool AddManualVerifyRecord(ref DataSet outputRecords, DataRow pers, string message, bool splitMonth=false)
        {
            if (string.IsNullOrEmpty(message) == false)
                G.AddNoteToDataRow(pers, message);

            if (splitMonth == true)
                outputRecords.Tables["ManualVerifyRecords"].Rows.Add(G.Field(pers, "EmployeeNumber"),
                                                                        G.Field(pers, "LastName"),
                                                                        G.Field(pers, "SSN"),
                                                                        G.Field(pers, "DeductionBenefitCode").Trim(),
                                                                        G.Field(pers, "PersClassification").Trim(),
                                                                        G.Field(pers, "TotalHours"),
                                                                        G.Field(pers, "DeductionCalcBasisAmount"),
                                                                        G.Field(pers, "CurrentAmountEmployer"),
                                                                        G.Field(pers, "CurrentAmountEmployee"),
                                                                        G.Field(pers, "EmployeeStatusCode").Trim(),
                                                                        G.Field(pers, "CheckAddMode").Trim(),
                                                                        G.Field(pers, "ReportPeriod"),
                                                                        G.Field(pers, "ReportNumber"),
                                                                        G.R1(pers, "ChargeDateTotalHours"),
                                                                        G.R2(pers, "ChargeDateTotalPay"),
                                                                        G.R1(pers, "Split1Hours"),
                                                                        G.R1(pers, "Split2Hours"),
                                                                        G.R3(pers, "Split1HoursRatio"),
                                                                        G.R3(pers, "Split2HoursRatio"),
                                                                        G.Field(pers, "Split1EarningPeriod"),
                                                                        G.Field(pers, "Split2EarningPeriod"),
                                                                        G.R2(pers, "Split1Pay"),
                                                                        G.R2(pers, "Split2Pay"),
                                                                        G.R4(pers, "Split1CompanyRate"),
                                                                        G.R4(pers, "Split2CompanyRate"),
                                                                        G.R4(pers, "Split1EmployeeRate"),
                                                                        G.R4(pers, "Split2EmployeeRate"),
                                                                        G.R2(pers, "Split1EmployeeContribution"),
                                                                        G.R2(pers, "Split2EmployeeContribution"),
                                                                        G.R2(pers, "Split1CompanyContribution"),
                                                                        G.R2(pers, "Split2CompanyContribution"),
                                                                        G.R2(pers, "CompanyContributionDifference"),
                                                                        G.R2(pers, "EmployeeContributionDifference"),
                                                                        G.Field(pers, "Note"));
            else
                outputRecords.Tables["ManualVerifyRecords"].Rows.Add(G.Field(pers, "EmployeeNumber"),
                                                                        G.Field(pers, "LastName"),
                                                                        G.Field(pers, "SSN"),
                                                                        G.Field(pers, "DeductionBenefitCode").Trim(),
                                                                        G.Field(pers, "PersClassification").Trim(),
                                                                        G.Field(pers, "TotalHours"),
                                                                        G.Field(pers, "DeductionCalcBasisAmount"),
                                                                        G.Field(pers, "CurrentAmountEmployer"),
                                                                        G.Field(pers, "CurrentAmountEmployee"),
                                                                        G.Field(pers, "EmployeeStatusCode").Trim(),
                                                                        G.Field(pers, "CheckAddMode").Trim(),
                                                                        G.Field(pers, "ReportPeriod"),
                                                                        G.Field(pers, "ReportNumber"),
                                                                        G.Field(pers, "EarningPeriod"),
                                                                        G.R1(pers, "ChargeDateTotalHours"),
                                                                        G.R2(pers, "ChargeDateTotalPay"),
                                                                        G.Field(pers, "Note"));
            return true;
        }


        /// <summary>
        /// DeriveSummaryAndInvoiceRecords
        /// </summary>
        /// <param name="outputRecords"></param>
        /// <param name="reportPeriod"></param>
        /// <param name="monthlyReportNumber"></param>
        /// <param name="expectedNumberOfMonthlyReports"></param>
        /// <returns></returns>
        public bool DeriveSummaryAndInvoiceRecords(ref DataSet outputRecords, string reportPeriod, string monthlyReportNumber, string expectedNumberOfMonthlyReports, bool splitMonth = false)
        {
            Debug.Assert(outputRecords != null);

            if ((outputRecords == null) ||
                (outputRecords.Tables.Contains("DefinedBenefitRecords") == false) ||
                (outputRecords.Tables.Contains("DefinedContributionRecords") == false) ||
                (outputRecords.Tables.Contains("SummaryRecord") == false) ||
                (outputRecords.Tables.Contains(PersByPeriodControlDate.TableName) == false))
                throw new Exception($"PersByPeriodControlDate::DeriveSummaryRecords : null argument.");

            // totals for DRS Summary Record
            decimal totalCompensation = 0.0m;
            decimal totalEmployeeAmt = 0.0m;
            decimal totalEmployerAmt = 0.0m;
            decimal totalHours = 0.0m;
            int totalRecords = 0;

            // totals for DRS Invoice Record
            decimal employerSharePlan1 = 0.0m;
            decimal employerSharePlan2 = 0.0m;
            decimal employerSharePlan3 = 0.0m;
            decimal totalEmployerShare = 0.0m;
            //--
            decimal employeeSharePlan1 = 0.0m;
            decimal employeeSharePlan2 = 0.0m;
            decimal employeeSharePlan3W = 0.0m;
            decimal employeeSharePlan3S = 0.0m;
            decimal totalEmployeeShare = 0.0m;
            //--
            decimal totalPERSContribution = 0.0m;

            // TODO : if incorporating MBR records above, then add those line-items to the summary row count here.

            //
            // Tabulate Defined Benefit Records (DBR) table
            //

            foreach (DataRow dbr in outputRecords.Tables["DefinedBenefitRecords"].Rows)
            {
                decimal compensation = G.Decimal(dbr, "Compensation", true);
                decimal employeeAmt = G.Decimal(dbr, "EmployeeAmt", true);
                decimal employerAmt = G.Decimal(dbr, "EmployerAmt", true);
                decimal hours = G.Decimal(dbr, "Hours", true);

                // For Summary Record
                totalCompensation += compensation;
                totalEmployeeAmt += employeeAmt;
                totalEmployerAmt += employerAmt;
                totalHours += hours;
                totalRecords++;

                // For Invoice Record
                string planCode = G.Field(dbr, "PlanCode");
                if (G.IsSame(planCode, "2"))
                {
                    employerSharePlan2 += employerAmt;
                    employeeSharePlan2 += employeeAmt;
                }
                else if (G.IsSame(planCode, "3"))
                {
                    employerSharePlan3 += employerAmt;
                }
                else if (G.IsSame(planCode, "1"))
                {
                    employerSharePlan1 += employerAmt;
                    employeeSharePlan1 += employeeAmt;
                }
            }
            Debug.Assert(totalRecords == outputRecords.Tables["DefinedBenefitRecords"].Rows.Count);

            //
            // Tabulate Defined Contribution Records (DCR) table
            //

            foreach (DataRow dcr in outputRecords.Tables["DefinedContributionRecords"].Rows)
            {
                decimal employeeAmt = G.Decimal(dcr, "EmployeeAmt", true);

                // For Summary Record
                totalEmployeeAmt += employeeAmt;
                totalRecords++;

                // For Invoice Record
                string optionCode = G.Field(dcr, "InvestProgram");
                if (G.IsSame(optionCode, "SELF"))
                {
                    employeeSharePlan3S += employeeAmt;
                }
                else if (G.IsSame(optionCode, "WSIB"))
                {
                    employeeSharePlan3W += employeeAmt;
                }
            }
            Debug.Assert(totalRecords == (outputRecords.Tables["DefinedBenefitRecords"].Rows.Count + outputRecords.Tables["DefinedContributionRecords"].Rows.Count));

            //
            //  Create the PERS Summary Record - i.e. top row of the DRS MRL report
            //

            outputRecords.Tables["SummaryRecord"].Rows.Add(reportPeriod,
                                                            monthlyReportNumber.PadLeft(2, '0'),
                                                            expectedNumberOfMonthlyReports.PadLeft(2, '0'),
                                                            G.R2(totalCompensation),
                                                            G.R2(totalEmployeeAmt),
                                                            G.R2(totalEmployerAmt),
                                                            G.R1(totalHours),
                                                            string.Format("{0}",totalRecords));

            totalEmployerShare = employerSharePlan1 + employerSharePlan2 + employerSharePlan3;
            totalEmployeeShare = employeeSharePlan1 + employeeSharePlan2 + employeeSharePlan3W + employeeSharePlan3S;
            totalPERSContribution = totalEmployerShare + totalEmployeeShare;

            //
            // Total any Register Adjustments - e.g. due to split-period rate changes which result in over/under deductions and contributions.
            // These adjustments will need to be either paid or deducted within the next PayPeriod.
            //
            
            decimal totalEmployeeRegisterAdjustment = 0.0m;
            decimal totalCompanyRegisterAdjustment = 0.0m;

            if (_PersConfiguration.IsSplitPeriodWithRateChanges())
            {
                foreach (DataRow rec in outputRecords.Tables[PersByPeriodControlDate.TableName].Rows)
                {
                    totalEmployeeRegisterAdjustment += G.Decimal(rec, "EmployeeContributionDifference", true);
                    totalCompanyRegisterAdjustment += G.Decimal(rec, "CompanyContributionDifference", true);
                }
            }

            //
            // Create the DRS Invoice Record - i.e. for comparison with the Payroll Register report and for Finance Dept Invoicing
            //

            outputRecords.Tables["DRSInvoice"].Rows.Add(G.R2(employerSharePlan1),
                                                        G.R2(employerSharePlan2),
                                                        G.R2(employerSharePlan3),
                                                        G.R2(totalEmployerShare),
                                                        G.R2(employeeSharePlan1),
                                                        G.R2(employeeSharePlan2),
                                                        G.R2(employeeSharePlan3W),
                                                        G.R2(employeeSharePlan3S),
                                                        G.R2(totalEmployeeShare),
                                                        G.R2(totalPERSContribution),
                                                        G.R2(totalEmployeeRegisterAdjustment),
                                                        G.R2(totalCompanyRegisterAdjustment));
            return true;
        }

    }
}
