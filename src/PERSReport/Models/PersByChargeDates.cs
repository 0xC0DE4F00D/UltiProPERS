using GenericParsing;
using PERSReport.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PERSReport.Models
{
    class PersByChargeDates 
    {
        ExceptionLog _Log;
        string _TimeTag;
        PersConfiguration _PersConfiguration;

        private IEnumerable<DataRow> _FirstPeriod_NonExempt_Records;
        private IEnumerable<DataRow> _SecondPeriod_NonExempt_Records;

        private IEnumerable<DataRow> _FirstPeriod_Exempt_Records;
        private IEnumerable<DataRow> _SecondPeriod_Exempt_Records;

        private IEnumerable<DataRow> _FirstPeriod_Commissioner_Records;
        private IEnumerable<DataRow> _SecondPeriod_Commissioner_Records;

        private IEnumerable<DataRow> _FirstPeriod_Retiree_Records;
        private IEnumerable<DataRow> _SecondPeriod_Retiree_Records;

        //private IEnumerable<DataRow> _NewEmployeeRecords;

        private string _CurrentReportPeriod;
        private string _CurrentReportNumber;

        private string _PriorEarningPeriod;
        private string _CurrentEarningPeriod;

        public const string TableName = @"ByChargeDates";

        public string CognosReportID { get; }

        public PersByChargeDates()
        {
            _Log = null;
            _TimeTag = string.Empty;

            _PersConfiguration = null;

            _FirstPeriod_NonExempt_Records = null;
            _SecondPeriod_NonExempt_Records = null;

            _FirstPeriod_Exempt_Records = null;
            _SecondPeriod_Exempt_Records = null;

            _FirstPeriod_Commissioner_Records = null;
            _SecondPeriod_Commissioner_Records = null;

            _FirstPeriod_Retiree_Records = null;
            _SecondPeriod_Retiree_Records = null;
            //_NewEmployeeRecords = null;

            CognosReportID = PERSReport.Properties.Settings.Default.RunPERSbyDailyHoursID;
            //CognosReportID = RunPERS.Properties.Settings.Default.RunPERSbyDailyHoursPATH;
        }

        public PersByChargeDates(ref DataSet inputRecords, ref ExceptionLog log, ref PersConfiguration persConfiguration, string timeTag)
        {
            _Log = log;
            _TimeTag = timeTag;
            _PersConfiguration = persConfiguration;

            _FirstPeriod_NonExempt_Records = null;
            _SecondPeriod_NonExempt_Records = null;

            _FirstPeriod_Exempt_Records = null;
            _SecondPeriod_Exempt_Records = null;

            _FirstPeriod_Commissioner_Records = null;
            _SecondPeriod_Commissioner_Records = null;

            _FirstPeriod_Retiree_Records = null;
            _SecondPeriod_Retiree_Records = null;
            //_NewEmployeeRecords = null;

            CognosReportID = PERSReport.Properties.Settings.Default.RunPERSbyDailyHoursID;
            //CognosReportID = RunPERS.Properties.Settings.Default.RunPERSbyDailyHoursPATH;
        }

        ~PersByChargeDates()
        {
            _Log = null;
            _TimeTag = string.Empty;
            _PersConfiguration = null;

            _FirstPeriod_NonExempt_Records = null;
            _SecondPeriod_NonExempt_Records = null;

            _FirstPeriod_Exempt_Records = null;
            _SecondPeriod_Exempt_Records = null;

            _FirstPeriod_Commissioner_Records = null;
            _SecondPeriod_Commissioner_Records = null;

            _FirstPeriod_Retiree_Records = null;
            _SecondPeriod_Retiree_Records = null;
            //_NewEmployeeRecords = null;
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

            if ((inputRecords.Tables.Contains(PersByChargeDates.TableName)) && (inputRecords.Tables[PersByChargeDates.TableName].Rows.Count > 0))
                inputRecords.Tables[PersByChargeDates.TableName].Clear();

            if (excel.GetWorksheetFromExcel(filePath, workSheetName, ref inputRecords, PersByChargeDates.TableName) == false)
                throw new Exception($"No records available within the specified file, {filePath}, or worksheet, {workSheetName}.\n");

            if (!(inputRecords.Tables[PersByChargeDates.TableName].Rows.Count > 0))
                throw new Exception($"No records imported from Cognos excel report file {filePath}.");

            return true;
        }


        public bool ParseCsvReportFile(ref DataSet inputRecords, string filePath, char fieldSeparator = ',')
        {
            Debug.Assert(inputRecords != null);

            Debug.WriteLine($"Parsing CSV report file: {filePath}");

            if (string.IsNullOrEmpty(filePath))
                throw new Exception("Please specify a valid data-file path.");

            if (inputRecords == null)
                throw new Exception("null argument");

            if ((inputRecords.Tables.Contains(PersByChargeDates.TableName)) && (inputRecords.Tables[PersByChargeDates.TableName].Rows.Count > 0))
                inputRecords.Tables[PersByChargeDates.TableName].Clear();

            DataSet ds = null;
            using (GenericParserAdapter parser = new GenericParserAdapter())
            {
                parser.SetDataSource(filePath);
                parser.ColumnDelimiter = fieldSeparator;
                parser.FirstRowHasHeader = true;
                ds = parser.GetDataSet();
                if ((ds != null) && (ds.Tables[0].Rows.Count > 0))
                {
                    ds.Tables[0].TableName = PersByChargeDates.TableName;
                    inputRecords.Tables.Add(ds.Tables[0].Copy());
                    ds.Clear();
                }
            }

            if (!(inputRecords.Tables[PersByChargeDates.TableName].Rows.Count > 0))
                throw new Exception($"No records imported from Cognos CSV report file {filePath}.");

            return true;
        }


        /// <summary>
        /// ProcessSplitMonthChargeDateRecords : Process ChargeDate report records on a split-month pay-period.
        /// </summary>
        /// <param name="inputRecords"></param>
        /// <param name="outputRecords"></param>
        /// <param name="payPeriodID"></param>
        /// <returns></returns>
        public bool ProcessSplitMonthChargeDateRecords(ref DataSet inputRecords, ref DataSet outputRecords)
        {
            if ((inputRecords == null) ||
                (outputRecords == null) ||
                (inputRecords.Tables.Contains(PersByPeriodControlDate.TableName) == false) ||
                (inputRecords.Tables.Contains(PersByChargeDates.TableName) == false))
                throw new Exception($"PersRecordParser::ProcessSplitMonthChargeDateRecords : null input arguments.");

            // Establish ReportPeriod and ReportNumber fields for each Split, prior and current. 
            _CurrentReportPeriod = _PersConfiguration.ReportPeriod();
            _CurrentReportNumber = _PersConfiguration.MonthlyReportNumber("G");

            _CurrentEarningPeriod = _PersConfiguration.EarningPeriod();
            _PriorEarningPeriod = _PersConfiguration.PriorEarningPeriod();

            // Aggregate by Exempt, NonExempt, and other employee categories.
            if (AggregateSplitMonthChargeDateRecords(ref inputRecords, ref outputRecords) == false)
                throw (new Exception("PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error aggregating split-month ChargeDate records."));

            // Parse Split-Period ChargeDate records and update custom schema columns within each PersByPeriodControlDate _InputRecords table row.
            foreach (DataRow pers in inputRecords.Tables[PersByPeriodControlDate.TableName].AsEnumerable())
            {
                string employeeNumber = G.Field(pers, "EmployeeNumber").Trim();

                pers.BeginEdit();
                    pers["ReportPeriod"] = _CurrentReportPeriod;
                    pers["ReportNumber"] = _CurrentReportNumber;
                    pers["ChargeDateTotalHours"] = 0.0m;
                    pers["ChargeDateTotalPay"] = 0.0m;
                    pers["Split1EarningPeriod"] = _PriorEarningPeriod;
                    pers["Split2EarningPeriod"] = _CurrentEarningPeriod;
                    pers["Split1Hours"] = 0.0m;
                    pers["Split2Hours"] = 0.0m;
                    pers["Split1HoursRatio"] = _PersConfiguration.Month1WorkDayRatio();
                    pers["Split2HoursRatio"] = _PersConfiguration.Month2WorkDayRatio();
                    pers["Split1Pay"] = 0.0m;
                    pers["Split2Pay"] = 0.0m;
                    pers["Split1CompanyRate"] = _PersConfiguration.Month1EmployerContributionRate(G.Field(pers, "DeductionBenefitCode").Trim());
                    pers["Split2CompanyRate"] = _PersConfiguration.Month2EmployerContributionRate(G.Field(pers, "DeductionBenefitCode").Trim());
                    pers["Split1EmployeeRate"] = _PersConfiguration.Month1MemberContributionRate(G.Field(pers, "DeductionBenefitCode").Trim());
                    pers["Split2EmployeeRate"] = _PersConfiguration.Month2MemberContributionRate(G.Field(pers, "DeductionBenefitCode").Trim());
                    pers["Split1CompanyContribution"] = 0.0m;
                    pers["Split2CompanyContribution"] = 0.0m;
                    pers["Split1EmployeeContribution"] = 0.0m;
                    pers["Split2EmployeeContribution"] = 0.0m;
                    pers["CompanyContributionDifference"] = 0.0m;
                    pers["EmployeeContributionDifference"] = 0.0m;
                pers.EndEdit();

                // Be considerate of the order here... Process Commissioners and Retirees first. Each of these could also be either Exempt or NonExempt. 
                // Hence, process their records only once by placing them first in this if-else sequence.

                if (IsCommissioner(pers))
                {
                    // First Split Period
                    if (AccumulateSplitChargeDates(employeeNumber, pers, ref _FirstPeriod_Commissioner_Records, true) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error accumulating ChargeDate sums for employee {employeeNumber}");

                    // Second Split Period
                    if (AccumulateSplitChargeDates(employeeNumber, pers, ref _SecondPeriod_Commissioner_Records, false) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error accumulating ChargeDate sums for employee {employeeNumber}");

                    // Summarize and Validate
                    if (Validate_COMMISSIONER_HoursAndPayTotals(employeeNumber, pers) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error validating ChargeDate versus PeriodControlDate sums for employee {employeeNumber}");
                }
                else if (IsRetiree(pers))
                {
                    // First Split Period
                    if (AccumulateSplitChargeDates(employeeNumber, pers, ref _FirstPeriod_Retiree_Records, true) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error accumulating ChargeDate sums for employee {employeeNumber}");

                    // Second Split Period
                    if (AccumulateSplitChargeDates(employeeNumber, pers, ref _SecondPeriod_Retiree_Records, false) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error accumulating ChargeDate sums for employee {employeeNumber}");

                    // Summarize and Validate
                    if (Validate_RETIREE_HoursAndPayTotals(employeeNumber, pers) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error validating ChargeDate versus PeriodControlDate sums for employee {employeeNumber}");
                }
                else if (IsNonExempt(pers))
                {
                    // First Split Period
                    if (AccumulateSplitChargeDates(employeeNumber, pers, ref _FirstPeriod_NonExempt_Records, true) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error accumulating ChargeDate sums for employee {employeeNumber}");

                    // Second Split Period
                    if (AccumulateSplitChargeDates(employeeNumber, pers, ref _SecondPeriod_NonExempt_Records, false) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error accumulating ChargeDate sums for employee {employeeNumber}");

                    // Summarize and Validate
                    if (Validate_NONEXEMPT_HoursAndPayTotals(employeeNumber, pers) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error validating ChargeDate versus PeriodControlDate sums for employee {employeeNumber}");
                }
                else if (IsExempt(pers))
                {
                    // First Split Period
                    if (AccumulateSplitChargeDates(employeeNumber, pers, ref _FirstPeriod_Exempt_Records, true) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error accumulating ChargeDate sums for employee {employeeNumber}");

                    // Second Split Period
                    if (AccumulateSplitChargeDates(employeeNumber, pers, ref _SecondPeriod_Exempt_Records, false) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error accumulating ChargeDate sums for employee {employeeNumber}");

                    // Summarize and Validate
                    if (Validate_EXEMPT_HoursAndPayTotals(employeeNumber, pers) == false)
                        throw new Exception($"PersByChargeDates::ProcessSplitMonthChargeDateRecords : Error validating ChargeDate versus PeriodControlDate sums for employee {employeeNumber}");
                }
            }
            return true;
        }

        private bool IsNonExempt(DataRow record)
        {
            string employeeTypeCode = G.Field(record, "EmployeeTypeCode");
            return ((employeeTypeCode.Contains("3") == false) 
                    && (employeeTypeCode.Contains("7") == false) 
                    && (employeeTypeCode.Contains("6") == false));
        }

        private bool IsExempt(DataRow record)
        {
            string employeeTypeCode = G.Field(record, "EmployeeTypeCode");
            return ((employeeTypeCode.Contains("3") == true)
                    || (employeeTypeCode.Contains("7") == true));
        }

        private bool IsCommissioner(DataRow record)
        {
            string employeeTypeCode = G.Field(record, "EmployeeTypeCode");
            return (employeeTypeCode.Contains("6") == true);
        }

        private bool IsRetiree(DataRow record)
        {
            string persClassification = G.Field(record, "PersClassification");
            return ((persClassification.Contains("98") == true)
                    || (persClassification.Contains("99") == true));
        }

        private bool IsNewEmployee(DataRow pers, out bool isHireDateAfterSplitDate)
        {
            isHireDateAfterSplitDate = false;
            DateTime lastHireDate;
            if (DateTime.TryParse(G.Field(pers, "LastHireDate"), out lastHireDate) == true)
            {
                if (lastHireDate >= _PersConfiguration.PayPeriodStartDate())
                {
                    if (lastHireDate >= _PersConfiguration.PayPeriodSplitDate())
                        isHireDateAfterSplitDate = true;
                    return true;
                }
            }
            return false;
        }



        private bool IsTerminated(DataRow pers, out bool isTermDateBeforeSplitDate)
        {
            isTermDateBeforeSplitDate = false;
            bool retval = (G.IsSame(G.Field(pers, "EmployeeStatusCode").Trim(), @"T"));

            if (retval)
            {
                DateTime termDate;
                if (DateTime.TryParse(G.Field(pers, "TerminationDate"), out termDate) == true)
                {
                    if (termDate < _PersConfiguration.PayPeriodSplitDate())
                    {
                        isTermDateBeforeSplitDate = true;
                    }
                }
            }
            return retval;
        }


        private bool AggregateSplitMonthChargeDateRecords(ref DataSet inputRecords, ref DataSet outputRecords)
        {
            Debug.Assert(inputRecords != null);
            Debug.Assert(outputRecords != null);

            if (inputRecords.Tables[PersByChargeDates.TableName].Rows.Count <= 0)
                throw new Exception($"PersByChargeDates::AggregateSplitMonthChargeDateRecords : missing PersByChargeDate report data.");

            string firstChargeMonth = _PersConfiguration.PayPeriodStartDate().Month.ToString("G");
            string secondChargeMonth = _PersConfiguration.PayPeriodEndDate().Month.ToString("G");

            // NON-EXEMPT
            // *** Query for Non-Exempt ChargeDate records from the FIRST period of split-month. 
            // Filter the following employee types:
            //     1. NOT Exempt employees - type 3* and type 7*
            //     2. NOT Commissioners - type 6
            //     3. NOT Retirees - PersClassification 99, 98
            IEnumerable<DataRow> query1 = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                          orderby record.Field<string>("EmployeeNumber") ascending
                                          where ((record.Field<string>("ChargeMonth").Equals(firstChargeMonth)) 
                                                && (record.Field<string>("EmployeeTypeCode").Contains("3") == false) 
                                                && (record.Field<string>("EmployeeTypeCode").Contains("7") == false)
                                                && (record.Field<string>("EmployeeTypeCode").Contains("6") == false)
                                                && (record.Field<string>("PersClassification").Contains("9") == false))
                                          select record;
            _FirstPeriod_NonExempt_Records = query1.ToArray();

            IEnumerable<DataRow> query2 = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                          orderby record.Field<string>("EmployeeNumber") ascending
                                          where ((record.Field<string>("ChargeMonth").Equals(secondChargeMonth)) 
                                          && (record.Field<string>("EmployeeTypeCode").Contains("3") == false) 
                                          && (record.Field<string>("EmployeeTypeCode").Contains("7") == false)
                                          && (record.Field<string>("EmployeeTypeCode").Contains("6") == false)
                                          && (record.Field<string>("PersClassification").Contains("9") == false))
                                          select record;
            _SecondPeriod_NonExempt_Records = query2.ToArray();

            // EXEMPT
            // *** Query for the Exempt Employee ChargeDate records for the FIRST period of split-month.
            // Filter following employee types:
            //     1. Non-Exempt employees - type 3* and NOT type 7*
            //     2. NOT Commissioners - type 6
            //     3. NOT Retirees - PersClassification 99, 98
            IEnumerable<DataRow> query3 = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                          orderby record.Field<string>("EmployeeNumber") ascending
                                          where ((record.Field<string>("ChargeMonth").Equals(firstChargeMonth))
                                          && ((record.Field<string>("EmployeeTypeCode").Contains("3") == true) || (record.Field<string>("EmployeeTypeCode").Contains("7") == true))
                                          && (record.Field<string>("EmployeeTypeCode").Contains("6") == false)
                                          && (record.Field<string>("PersClassification").Contains("9") == false))
                                          select record;
            _FirstPeriod_Exempt_Records = query3.ToArray();

            IEnumerable<DataRow> query4 = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                          orderby record.Field<string>("EmployeeNumber") ascending
                                          where ((record.Field<string>("ChargeMonth").Equals(secondChargeMonth))
                                          && ((record.Field<string>("EmployeeTypeCode").Contains("3") == true) || (record.Field<string>("EmployeeTypeCode").Contains("7") == true))
                                          && (record.Field<string>("EmployeeTypeCode").Contains("6") == false)
                                          && (record.Field<string>("PersClassification").Contains("9") == false))
                                          select record;
            _SecondPeriod_Exempt_Records = query4.ToArray();

            // COMMISSIONERS
            // *** Query for the Commissioner records 
            // Filter the following employee types:
            //     1. Commissioners - type 6
            IEnumerable<DataRow> query5 = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                          orderby record.Field<string>("EmployeeNumber") ascending
                                          where ((record.Field<string>("ChargeMonth").Equals(firstChargeMonth))
                                          && (record.Field<string>("EmployeeTypeCode").Contains("6") == true))
                                          select record;
            _FirstPeriod_Commissioner_Records = query5.ToArray();

            IEnumerable<DataRow> query6 = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                          orderby record.Field<string>("EmployeeNumber") ascending
                                          where ((record.Field<string>("ChargeMonth").Equals(secondChargeMonth))
                                          && ((record.Field<string>("EmployeeTypeCode").Contains("6") == true)))
                                          select record;
            _SecondPeriod_Commissioner_Records = query6.ToArray();

            // RETIREES
            // *** Query for the Retiree records 
            // Filter the following employee types:
            //     1. Retirees - PersClassification 99, 98
            IEnumerable<DataRow> query7 = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                          orderby record.Field<string>("EmployeeNumber") ascending
                                          where ((record.Field<string>("ChargeMonth").Equals(firstChargeMonth))
                                          && ((record.Field<string>("PersClassification").Contains("98") == true)
                                          || (record.Field<string>("PersClassification").Contains("99") == true)))
                                          select record;
            _FirstPeriod_Retiree_Records = query7.ToArray();

            IEnumerable<DataRow> query8 = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                          orderby record.Field<string>("EmployeeNumber") ascending
                                          where ((record.Field<string>("ChargeMonth").Equals(secondChargeMonth))
                                          && ((record.Field<string>("PersClassification").Contains("98") == true)
                                          || (record.Field<string>("PersClassification").Contains("99") == true)))
                                          select record;
            _SecondPeriod_Retiree_Records = query8.ToArray();

#if DEBUG
            int sumOfAggregateRecords = _FirstPeriod_NonExempt_Records.Count() +
                                        _SecondPeriod_NonExempt_Records.Count() +
                                        _FirstPeriod_Exempt_Records.Count() +
                                        _SecondPeriod_Exempt_Records.Count() +
                                        _FirstPeriod_Commissioner_Records.Count() +
                                        _SecondPeriod_Commissioner_Records.Count() +
                                        _FirstPeriod_Retiree_Records.Count() +
                                        _SecondPeriod_Retiree_Records.Count();
            Debug.Assert(sumOfAggregateRecords == inputRecords.Tables[PersByChargeDates.TableName].Rows.Count);
#endif

            // *** Query for the New Employee records.
            //IEnumerable<DataRow> queryX = from record in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
            //                              orderby record.Field<string>("EmployeeNumber") ascending
            //                              where (_PayrollCalendar.IsDateWithinPayPeriod(payPeriodID, G.Field(record, "LastHireDate")) == true)
            //                              select record;
            //_NewEmployeeRecords = queryX.ToArray();

            return true;
        }


        private bool AccumulateSplitChargeDates(string employeeNumber, DataRow pers, ref IEnumerable<DataRow> splitChargeDateRecords, bool firstSplit)
        {
            // Accumulate ChargeDate CurrentHours and CurrentAmount per employee (i.e. pay) by qualifying "PERSxAccountable" columns within the ChargeDate Report.
            //
            // Don't necessarily trust the ChargeDate earnings (i.e. CurrentAmount) summation. Use the PeriodControlDate "DeductionCalcBasisAmount" instead.
            // The ChargeDate report doesn't include all earnings (e.g. IsolationPay) and certain employee classes (e.g. Retirees) do not
            // have PERS deductions but we still must report "TotalEarningAmount".
            //
            // DRS "TotalPay" is based upon employee PERS Classification and upon employee type.
            // Normally, the "DeductionCalcBasisAmount" is the employees "TotalPay" from a PERS perspective.
            // Exceptions to this include Retirees (whom need only report "TotalHoursWorked" and "TotalEarningAmount").
            //
            // January, 2021.  A new PERS classification code is established as follows:
            //        Deduction: PERS3 New Hire Contribution 0    DeductionBenefitCode: P3NH
            // This represents a new-hire with a prior PERS3 contribution history who has not yet chosen contribution plan options.
            // DRS requires corresponding total compensation reporting, but not 'TotalEarningAmount'; rather 'DeductionCalcBasisAmount'.
            // Interestingly, the DeductionCalcBasisAmount is zero on P3NH classified employee paychecks (and thus within the PERSByPeriodControlDate report).
            // We can instead derive a DeductionCalcBasisAmount value by summing qualifying PERS compensation from the PERSByChargeDate 'CurrentAmount'
            // column. This will suffice in most cases, except wherein the employee's total compensation (and thus DeductionCalcBasisAmount) includes any
            // non-regular earnings, such as 'Isolation Pay'.
            //
            // March, 2021. A Terminated employee may receive pay (i.e. by ChargeDate) past the actual TerminationDate.  If this pay is accounted
            // after the TerminationDate, and specifically, after the SplitPeriod end-date, then it will show-up in the wrong DRS EarningPeriod.
            // Hence, flag this condition for subsequent movement of earnings and contributions into the appropriate EarningPeriod.

            decimal totalHours = 0.0m;
            decimal totalPay = 0.0m;
            decimal totalTerminationPay = 0.0m;
            decimal totalTerminationHours = 0.0m;

            // Get specified employee records from the ChargeDateRecords but only those that are PERS plan accountable by employee plan participation.

            string dbc = G.Field(pers, "DeductionBenefitCode").Trim();
            IEnumerable<DataRow> empChargeRecs = null;

            if (G.IsSame(dbc, "PERS2"))
            {
                var queryP2 = from r in splitChargeDateRecords
                                 where ((r.Field<string>("EmployeeNumber").Trim().Equals(employeeNumber)) &&                                   
                                        (r.Field<string>("Pers2Accountable").Contains("Y")))
                                 select r;
                empChargeRecs = queryP2.ToArray();
            }
            else if (G.Contains(dbc, "P3"))
            {
                var queryP3 = from r in splitChargeDateRecords
                              where ((r.Field<string>("EmployeeNumber").Trim().Equals(employeeNumber)) &&
                                     (r.Field<string>("Pers3Accountable").Contains("Y")))
                              select r;
                empChargeRecs = queryP3.ToArray();
            }
            else if (G.IsSame(dbc, "PERS1"))
            {
                var queryP1 = from r in splitChargeDateRecords
                              where ((r.Field<string>("EmployeeNumber").Trim().Equals(employeeNumber)) &&
                                     (r.Field<string>("Pers1Accountable").Contains("Y")))
                              select r;
                empChargeRecs = queryP1.ToArray();
            }
            else if (G.IsSame(dbc, "PERS0"))   // PERS retirees who are still working.
            {
                var queryP0 = from r in splitChargeDateRecords
                                 where ((r.Field<string>("EmployeeNumber").Trim().Equals(employeeNumber)) &&
                                        (r.Field<string>("Pers1Accountable").Contains("Y")))
                                 select r;
                empChargeRecs = queryP0.ToArray();
            }
            else
            {
                G.DisplayWarning($"{employeeNumber} : Employee is missing PERS Plan Code : DeductionBenefitCode={dbc}");
                G.AddNoteToDataRow(pers, $"Employee is missing PERS Plan Code : DeductionBenefitCode={dbc}");
            }

            if ((empChargeRecs == null) || (empChargeRecs.Count() == 0))   // No ChargeDate records found for this employee for this month-Split
            {
                if (IsNonExempt(pers) == true)
                {
                    if (firstSplit == true)
                        G.AddNoteToDataRow(pers, $"No ChargeDate records found for split1");
                    else
                        G.AddNoteToDataRow(pers, $"No ChargeDate records found for split2");
                }
                return true;
            }
            else 
            {
                // Accumulate only PERS "eligible" hours and pay per ChargeDate report; see the "PERS?Accountable" columns therein.   
                foreach (DataRow e in empChargeRecs)
                {
                    if ((G.IsDiff(employeeNumber, G.Field(e, "EmployeeNumber")) == true))
                        throw new Exception($"PersByChargeDates::AccumulateSplitChargeDates : Employee ID mismatch : {employeeNumber} : {G.Field(e, "EmployeeNumber")}");

                    // Check for any charge date that occurs past a TerminationDate and into the second EarningPeriod; employee may receive pay past this date.  
                    // Issue a warning. And, modify hours, pay, and contributions to occur on or before the TerminationDate.
                    if ((G.IsSame("T", G.Field(e, "EmployeeStatusCode")) == true) && (firstSplit == false))
                    {
                        DateTime terminationDate;
                        DateTime chargeDate;
                        if ((DateTime.TryParse(G.Field(e, "TerminationDate"), out terminationDate) == true) &&
                            (DateTime.TryParse(G.Field(e, "ChargeDate"), out chargeDate) == true))
                        {
                            if (chargeDate > terminationDate)
                            {
                                G.DisplayWarning($"{employeeNumber} : Pay received past Termination date : ChargeDate={chargeDate.ToShortDateString()} : TerminationDate={terminationDate.ToShortDateString()}");
                                G.AddNoteToDataRow(pers, $"*** Pay received past Termination date and within second EarningPeriod. Added to first EarningPeriod. *** ");
                                totalTerminationHours += decimal.Parse(G.Field(e, "CurrentHours"));
                                totalTerminationPay += decimal.Parse(G.Field(e, "CurrentAmount"));
                            }
                            else
                            {
                                totalHours += decimal.Parse(G.Field(e, "CurrentHours"));
                                totalPay += decimal.Parse(G.Field(e, "CurrentAmount"));
                            }
                        }
                        else
                            throw new Exception($"PersByChargeDates::AccumulateSplitChargeDates : Termination/Charge Date read error : {employeeNumber} ");
                    }
                    else  // normal case...
                    {
                        totalHours += decimal.Parse(G.Field(e, "CurrentHours"));
                        totalPay += decimal.Parse(G.Field(e, "CurrentAmount"));
                    }
                }
            }

            if (firstSplit == true)
            {
                pers.BeginEdit();
                    pers["Split1Hours"] = totalHours + totalTerminationHours;
                    pers["Split1Pay"] = totalPay + totalTerminationPay;
                pers.EndEdit();
            }
            else // secondSplit
            {
                pers.BeginEdit();
                    pers["Split2Hours"] = totalHours + totalTerminationHours;
                    pers["Split2Pay"] = totalPay + totalTerminationPay;
                pers.EndEdit();
            }
            return true;
        }


        private bool Validate_NONEXEMPT_HoursAndPayTotals(string employeeNumber, DataRow pers, bool IsRetiree=false)
        {
            // DRS "TotalPay" is based upon employee PERS Classification and upon employee type.
            // Normally, the "DeductionCalcBasisAmount" is the employees "TotalPay" from a PERS perspective.
            // Exceptions to this include Retirees (whom need only report "TotalHoursWorked" and "TotalEarningAmount").
            //
            // NOTE: January, 2021.  A new PERS classification code is established as follows:
            //        Deduction: PERS3 New Hire Contribution 0    DeductionBenefitCode: P3NH
            // This represents a new-hire with a prior PERS3 contribution history who has not yet chosen contribution plan options.
            // DRS requires corresponding total compensation reporting, but not 'TotalEarningAmount'; rather 'DeductionCalcBasisAmount'.
            // Interestingly, the DeductionCalcBasisAmount is zero on P3NH classified employee paychecks (and thus within the PERSByPeriodControlDate report).
            // We can instead derive a DeductionCalcBasisAmount value by summing qualifying PERS qualifying compensation from the PERSByChargeDate 'CurrentAmount'
            // column. This will suffice in most cases, except wherein the employee's total compensation (and thus DeductionCalcBasisAmount) includes any
            // non-regular earnings, such as 'Isolation Pay'.

            // June 2021 : An issue was observed with hours splitting ratio calculations. The issue arises when a Retro Pay Adjustment is 
            // initiated within the payroll processing after WFM Payroll Export, and the adjustment introduces a significant hours correction. 
            // An example is a Retro supporting Short-Term-Disability (STD) which introduces a large negative adjustment with all adjusted hours falling into the last
            // day of the pay-period. From a Charge Date perspective, this adjustment skews the splitHoursRatio derived value while also reducing 
            // PERSAccountable total hours. We end-up with derived split-period values for hours and contributions that incorrectly represents the qualifying values.
            // Run the report for check-date of 6/17/2021 in order to see representative applicable records. 
            // ... Not every Retro Adjustment scenario can be predicted herein. But, we can detect an imbalance in the split?HoursRatio resulting
            // from an over-weighted end-of-period pay adjustment and introduce a re-balancing via use of the default ratio of 8 hours per day by 80 hours per period.

            // June 2021 : DRS introduced both an Employee Deduction Rate change within the split-period as well as the Employer Contribution Rate change.
            // The input to this mechanism is the PersConfiguration.json file which must match the DRS published Rate Change Schedule.  See file PersConfiguration.cs

            // July 2021 : Thoughts on PERS-able Hours for NON-EXEMPT employees:
            // 1. UltiPro does provide actual hours worked per day (timesheet-wise) for NON-EXEMPT employees. 
            // 2. But, the Charge Date report also infrequently introduces non-actual hours to account for Retro's or other + or - line items.
            // 3. Normally, we use the actual hours worked per day for split ratio and totals derivation.
            // 4. However, when an abnormal scenario is encountered, we use the default ratio of 8 hours per workday and 10 workdays per pay-period.
            // 5. For new-hires and terminations wherein a split Earning Period occurs, all hours may fall into only 1 split period. See line ~536 above within the AccumulateSplitChargeDates method.
            // 6. Adjustments to Charge Date hours (e.g. Retro + or - line items) may significantly alter split ratios and totals. Hence, herein, we seek to normalize 
            //    ratios to the default ratio when an hours per split period imbalance is determined (e.g. negative split hours).

            //
            // *** Validate accumulated per day hours and corresponding ratios per period split.
            //

            // From the PeriodControlDate report...
            string dbc = G.Field(pers, "DeductionBenefitCode").Trim();
            decimal deductionCalcBasisAmount = G.Decimal(pers, "DeductionCalcBasisAmount");
            decimal totalHoursPCD = G.Decimal(pers, "TotalHours");

            if (IsRetiree)
            {
                deductionCalcBasisAmount = G.Decimal(pers, "TotalEarningAmount");
                G.AddNoteToDataRow(pers, $"Retiree : TotalEarningAmount={G.R2(deductionCalcBasisAmount)}");
            }

            // For a PERS3 New Hire with no corresponding employee deduction, use their Charge Date report pay totals as the DeductionCalcBasisAmount instead.
            if ((G.IsSame(dbc, "P3NH") == true) && (deductionCalcBasisAmount == 0.0m))
            {
                deductionCalcBasisAmount = G.Decimal(pers, "Split1Pay") + G.Decimal(pers, "Split2Pay");
                G.AddNoteToDataRow(pers, $"PERS3 New Hire : Derived DeductionCalcBasisAmount={G.R2(deductionCalcBasisAmount)}");
            }

            // Values derived from above ChargeDate records accumulate processing above...
            decimal split1Hours = G.Decimal(pers, "Split1Hours");
            decimal split2Hours = G.Decimal(pers, "Split2Hours");

            decimal totalHoursChgDt = split1Hours + split2Hours;                // from ChargeDate PERSaccountable accumulations above.
            decimal split1HoursRatio = G.Decimal(pers, "Split1HoursRatio");     // Default is preset above to an 8/80 daily hours ratio, see lines around 212 above.
            decimal split2HoursRatio = G.Decimal(pers, "Split2HoursRatio");

            // For NON-EXEMPT, try to use the actual charge-date hours totals for split period ratio calculation - normal case

            if ((split1Hours >= 0) && (split2Hours >= 0) && (totalHoursChgDt > 0))
            {
                // With non-EXEMPT, use the actual hours per day ratio instead of the (8 hours per day) / (80 hours per period) default ratio.
                // ChargeDate hours are normally actual and accurate for hourly employees since we start with a detailed Payroll Export file
                // from WFM for Non-EXEMPT employees.  Negative hours indicate an abnormal condition (e.g. Retro Adjustment) and should not
                // be trusted for ratio calculations. 
                split1HoursRatio = Math.Abs(split1Hours / totalHoursChgDt);
                split2HoursRatio = Math.Abs(split2Hours / totalHoursChgDt);
                pers.BeginEdit();
                    pers["Split1HoursRatio"] = split1HoursRatio;
                    pers["Split2HoursRatio"] = split2HoursRatio;
                pers.EndEdit();
            }
            else
            {
                // Use the default SplitNHoursRatio, but log the condition for manual investigation - see the "ManualValidation" worksheet within the Validation Report.
                G.DisplayWarning($"{employeeNumber} : Validate ChargeDate hours: {G.R2(totalHoursChgDt)}.");
                G.AddNoteToDataRow(pers, $"Validate ChargeDate hours: {G.R2(totalHoursChgDt)}. ");
            }

            if (G.HasSignificantDifference(totalHoursChgDt, totalHoursPCD) == true)
            {
                G.AddNoteToDataRow(pers, $"ChargeDate TotalHours and PeriodControlDate TotalHours differ significantly. ");
            }

            //
            // *** Now, use the split ratio to derive pay and contribution splits.
            //

            decimal split1Pay = split1HoursRatio * deductionCalcBasisAmount;
            decimal split2Pay = split2HoursRatio * deductionCalcBasisAmount;
            decimal totalPay = split1Pay + split2Pay;

            if (G.HasMinimalDifference(deductionCalcBasisAmount, totalPay) == false)
            {
                G.DisplayWarning($"{employeeNumber} : ERROR : DeductionCalcBasisAmount differs from SplitSum : SUM={G.R2(totalPay)} : DCBA={G.R2(deductionCalcBasisAmount)}");
                G.AddNoteToDataRow(pers, $"ERROR DeductionCalcBasisAmount differs from SplitSum : SUM={G.R2(totalPay)} : DCBA={G.R2(deductionCalcBasisAmount)}");
            }

            //
            // **** Split the Employee Contribution amount by work-day ratio and any rate difference between earning periods.
            //

            decimal currentEmployeeContribution = G.Decimal(pers, "CurrentAmountEmployee");
            decimal split1EmployeeRate = G.Decimal(pers, "Split1EmployeeRate");
            decimal split2EmployeeRate = G.Decimal(pers, "Split2EmployeeRate");
            decimal split1EmployeeContribution;
            decimal split2EmployeeContribution;
            decimal derivedEmployeeContribution;

            if ((G.HasMinimalDifference(split1EmployeeRate, split2EmployeeRate) == true) || (G.Contains(dbc, "P3")))
            {
                // When there is no rate-change between earning periods OR the employee is a PERS3 participant, then simply split the contribution amount by the hours ratio.
                split1EmployeeContribution = currentEmployeeContribution * split1HoursRatio;
                split2EmployeeContribution = currentEmployeeContribution * split2HoursRatio;
                derivedEmployeeContribution = split1EmployeeContribution + split2EmployeeContribution;
                if (G.HasMinimalDifference(currentEmployeeContribution, derivedEmployeeContribution) == false)
                {
                    G.DisplayWarning($"{employeeNumber} : ERROR : Employee Contribution differs on Split : SPLIT={G.R2(derivedEmployeeContribution)} : ORIG={G.R2(currentEmployeeContribution)}");
                    G.AddNoteToDataRow(pers, $"ERROR : Employee Contribution differs on Split : SPLIT={G.R2(derivedEmployeeContribution)} : ORIG={G.R2(currentEmployeeContribution)}");
                }
            }
            else
            {
                // When there is a rate change between earning periods, then calculate the new contribution amounts and log any difference between contribution totals.
                split1EmployeeContribution = split1EmployeeRate * (deductionCalcBasisAmount * split1HoursRatio);
                split2EmployeeContribution = split2EmployeeRate * (deductionCalcBasisAmount * split2HoursRatio);
                derivedEmployeeContribution = split1EmployeeContribution + split2EmployeeContribution;
                if (G.HasMinimalDifference(currentEmployeeContribution, derivedEmployeeContribution) == false)
                {
                    pers.BeginEdit();
                        pers["EmployeeContributionDifference"] = -(currentEmployeeContribution - derivedEmployeeContribution);
                    pers.EndEdit();
                    //G.AddNoteToDataRow(pers, $"RateChange : Employee Contribution imbalance : SPLIT={G.R2(derivedEmployeeContribution)} : ORIG={G.R2(currentEmployeeContribution)}");
                }
            }

            //
            // **** Split the Company contribution amount by the work-day ratio and/or any difference in rates per split-period.
            //

            decimal currentCompanyContribution = G.Decimal(pers, "CurrentAmountEmployer");
            decimal split1CompanyRate = G.Decimal(pers, "Split1CompanyRate");
            decimal split2CompanyRate = G.Decimal(pers, "Split2CompanyRate");
            decimal split1CompanyContribution;
            decimal split2CompanyContribution;
            decimal derivedCompanyContribution;

            if ((G.HasMinimalDifference(split1CompanyRate, split2CompanyRate) == true))
            {
                // When there is no rate-change between earning periods, then simply split the benefit amount by the hours ratio.
                split1CompanyContribution = currentCompanyContribution * split1HoursRatio;
                split2CompanyContribution = currentCompanyContribution * split2HoursRatio;
                derivedCompanyContribution = split1CompanyContribution + split2CompanyContribution;
                if (G.HasMinimalDifference(currentCompanyContribution, derivedCompanyContribution) == false)
                {
                    G.DisplayWarning($"{employeeNumber} : ERROR : Company Contribution differs on Split : SPLIT={G.R2(derivedCompanyContribution)} : ORIG={G.R2(currentCompanyContribution)}");
                    G.AddNoteToDataRow(pers, $"ERROR : Company Contribution differs on Split : SPLIT={G.R2(derivedCompanyContribution)} : ORIG={G.R2(currentCompanyContribution)}");
                }
            }
            else
            {
                // When there is a rate change between earning periods, then calculate the new contribution amounts and log any difference between contribution totals.
                split1CompanyContribution = split1CompanyRate * (deductionCalcBasisAmount * split1HoursRatio);
                split2CompanyContribution = split2CompanyRate * (deductionCalcBasisAmount * split2HoursRatio);
                derivedCompanyContribution = split1CompanyContribution + split2CompanyContribution;
                if (G.HasMinimalDifference(currentCompanyContribution, derivedCompanyContribution) == false)
                {
                    pers.BeginEdit();
                        pers["CompanyContributionDifference"] = -(currentCompanyContribution - derivedCompanyContribution);
                    pers.EndEdit();
                    //G.AddNoteToDataRow(pers, $"RateChange : Company Contribution imbalance : SPLIT={G.R2(derivedCompanyContribution)} : ORIG={G.R2(currentCompanyContribution)}");
                }
            }

            //
            // Update the PeriodControlDate Record for this employee.
            // 

            pers.BeginEdit();
                pers["ChargeDateTotalHours"] = totalHoursChgDt;
                pers["ChargeDateTotalPay"] = G.Decimal(pers, "Split1Pay") + G.Decimal(pers, "Split2Pay");  
                //
                pers["Split1Pay"] = split1Pay;
                pers["Split1EmployeeContribution"] = split1EmployeeContribution;
                pers["Split1CompanyContribution"] = split1CompanyContribution;
                //
                pers["Split2Pay"] = split2Pay;
                pers["Split2EmployeeContribution"] = split2EmployeeContribution;
                pers["Split2CompanyContribution"] = split2CompanyContribution;
            pers.EndEdit();

            return true;
        }


        private bool Validate_EXEMPT_HoursAndPayTotals(string employeeNumber, DataRow pers, bool IsRetiree=false)
        {
            // DRS "TotalPay" is based upon employee PERS Classification and upon employee type.
            // Normally, the "DeductionCalcBasisAmount" is the employees "TotalPay" from a PERS perspective.
            // Exceptions to this include Retirees (whom need only report "TotalHoursWorked" and "TotalEarningAmount").
            //
            // January, 2021.  A new PERS classification code is established as follows:
            //        Deduction: PERS3 New Hire Contribution 0    DeductionBenefitCode: P3NH
            // This represents a new-hire with a prior PERS3 contribution history who has not yet chosen contribution plan options.
            // DRS requires corresponding total compensation reporting, but not 'TotalEarningAmount'; rather 'DeductionCalcBasisAmount'.
            // Interestingly, the DeductionCalcBasisAmount is zero on P3NH classified employee paychecks (and thus within the PERSByPeriodControlDate report).
            // We can instead derive a DeductionCalcBasisAmount value by summing qualifying PERS qualifying compensation from the PERSByChargeDate 'CurrentAmount'
            // column. This will suffice in most cases, except wherein the employee's total compensation (and thus DeductionCalcBasisAmount) includes any
            // non-regular earnings, such as 'Isolation Pay'.

            // June 2021 : An issue was observed with hours splitting ratio calculations. The issue arises when a Retro Pay Adjustment is 
            // initiated within the payroll processing after WFM Payroll Export, and the adjustment introduces a significant hours correction. 
            // An example is a Retro supporting Short-Term-Disability (STD) which introduces a large negative adjustment with all adjusted hours falling into the last
            // day of the pay-period. From a Charge Date perspective, this adjustment skews the splitHoursRatio derived value while also reducing 
            // PERSAccountable total hours. We end-up with derived split-period values for hours and contributions that incorrectly represents the qualifying values.
            // Run the report for check-date of 6/17/2021 in order to see representative applicable records. 
            // ... Not every Retro Adjustment scenario can be predicted herein. But, we can detect an imbalance in the split?HoursRatio resulting
            // from an over-weighted end-of-period pay adjustment and introduce a re-balancing via use of the default ratio of 8 hours per day by 80 hours per period.

            // June 2021 : DRS introduced both an Employee Deduction Rate change within the split-period as well as the Employer Contribution Rate change.
            // The input to this mechanism is the PersConfiguration.json file which must match the DRS published Rate Change Schedule.  See file PersConfiguration.cs

            // July 2021 : Thoughts on PERS-able Hours for EXEMPT employees:
            // 1. UltiPro doesn't provide actual hours worked per day (timesheet-wise) for EXEMPT employees (these hours are not exported from WFM during Payroll Processing).
            // 2. We use the default ratio of 8 hours per workday and 10 workdays per pay-period.
            // 3. No consideration is given to an EXEMPT employee working fewer than 10 workdays per pay-period, except for new-hires and terminations wherein a 
            //    split Earning Period occurs. See line ~536 above within the AccumulateSplitChargeDates method.
            // 4. Adjustments to Charge Date hours (e.g. Retro + or - line items) may significantly alter split ratios and totals. Hence, herein, we seek to normalize 
            //    ratios to the default ratio in all EXEMPT employee cases.

            // September 2021 : Issue with Terminations who have no actual work-hours within the pay-period, only cash-outs...
            // 1. It was observed that Exempt employee terminations could have only cash-out hours & pay with all hours falling into Split2 alone.
            //    And, this cash-out could occur on a date subsequent to the actual termination date.  Hence, we adjust the hours per split-period
            //    default ratio so that final cash-out hours & pay are distributed across split1 and split2 accordingly.
            //

            //
            // *** Validate accumulated per day hours and corresponding ratios per period split.
            //

            // From the PeriodControlDate report...
            string dbc = G.Field(pers, "DeductionBenefitCode").Trim();
            decimal deductionCalcBasisAmount = G.Decimal(pers, "DeductionCalcBasisAmount");
            decimal totalHoursPCD = G.Decimal(pers, "TotalHours");

            if (IsRetiree)
            {
                deductionCalcBasisAmount = G.Decimal(pers, "TotalEarningAmount");
                G.AddNoteToDataRow(pers, $"Retiree : TotalEarningAmount={G.R2(deductionCalcBasisAmount)}");
            }

            // For a PERS3 New Hire with no corresponding employee deduction, use their Charge Date report pay totals as the DeductionCalcBasisAmount instead.
            if ((G.IsSame(dbc, "P3NH") == true) && (deductionCalcBasisAmount == 0.0m))
            {
                deductionCalcBasisAmount = G.Decimal(pers, "Split1Pay") + G.Decimal(pers, "Split2Pay");
                G.AddNoteToDataRow(pers, $"PERS3 New Hire : DeductionCalcBasisAmount={G.R2(deductionCalcBasisAmount)}");
            }

            // Values derived above ChargeDate records processing...
            decimal split1Hours = G.Decimal(pers, "Split1Hours");
            decimal split2Hours = G.Decimal(pers, "Split2Hours");
            decimal split1HoursRatio = G.Decimal(pers, "Split1HoursRatio");     // Default is preset above to an 8/80 daily hours ratio, see lines around 212 above.
            decimal split2HoursRatio = G.Decimal(pers, "Split2HoursRatio");
            decimal totalHoursChgDt = split1Hours + split2Hours;

            // For EXEMPT employees, apply the default splitXHoursRatio to the totalHoursChgDt in order (since we don't have per-day PERS-able hours),
            // except when the employee is Terminated or a NewHire.

            bool isTermDateBeforeSplitDate;
            bool isHireDateAfterSplitDate;
            
            if (IsTerminated(pers, out isTermDateBeforeSplitDate) == true)
            {
                // Termination hours should already be adjusted to split period as needed above during accumulation.
                // But, for Exempt employees, real hours worked are not available within the Charge Date report; only PLP or cashouts.
                // And, typically, the cashout hours & pay are allocated only at pay-period ending. Thus, we could encounter a 
                // situation in which split1 has zero hours and split2 has all PERS qualifying hours & pay.
                // Or, the termination date occurs before the split date but all PERS qualifying hours & pay appear after the term date.
                // Distribute the total hours between Split1 and Split2 per the default 8/80 daily hours ratio for the first case
                // and reset the ratio for the second case.

                if (isTermDateBeforeSplitDate)
                {
                    split1HoursRatio = 1.0m;
                    split2HoursRatio = 0.0m;
                    pers.BeginEdit();
                        pers["Split1Hours"] = split1HoursRatio * totalHoursChgDt;
                        pers["Split2Hours"] = split2HoursRatio * totalHoursChgDt;
                        pers["Split1HoursRatio"] = split1HoursRatio;
                        pers["Split2HoursRatio"] = split2HoursRatio;
                    pers.EndEdit();
                }
                else
                {
                    split1Hours = split1HoursRatio * totalHoursChgDt;
                    split2Hours = split2HoursRatio * totalHoursChgDt;
                    pers.BeginEdit();
                        pers["Split1Hours"] = split1Hours;
                        pers["Split2Hours"] = split2Hours;
                    pers.EndEdit();
                }
            }
            else if ((IsNewEmployee(pers, out isHireDateAfterSplitDate) == true) && (isHireDateAfterSplitDate))
            {
                // Did employment start in Split2 Earning Period?  Then all hours & pay should be set only in Split2.
                split1Hours = 0.0m;
                split2Hours = totalHoursChgDt;
                split1HoursRatio = 0.0m;
                split2HoursRatio = 1.0m;
                pers.BeginEdit();
                    pers["Split1Hours"] = split1Hours;
                    pers["Split2Hours"] = split2Hours;
                    pers["Split1HoursRatio"] = split1HoursRatio;
                    pers["Split2HoursRatio"] = split2HoursRatio;
                pers.EndEdit();
            }
            else  // normal case
            {
                split1Hours = split1HoursRatio * totalHoursChgDt;
                split2Hours = split2HoursRatio * totalHoursChgDt;
                pers.BeginEdit();
                    pers["Split1Hours"] = split1Hours;
                    pers["Split2Hours"] = split2Hours;
                pers.EndEdit();
            }

            if (G.HasSignificantDifference(totalHoursChgDt, totalHoursPCD) == true)
            {
                G.AddNoteToDataRow(pers, $"ChargeDate TotalHours and PeriodControlDate TotalHours differ significantly. ");
            }

            //
            // *** Now, use the default or actual split ratio to derive pay and contribution splits.
            //

            decimal split1Pay = split1HoursRatio * deductionCalcBasisAmount;
            decimal split2Pay = split2HoursRatio * deductionCalcBasisAmount;
            decimal totalPay = split1Pay + split2Pay;

            if (G.HasMinimalDifference(deductionCalcBasisAmount, totalPay) == false)
            {
                G.DisplayWarning($"{employeeNumber} : DeductionCalcBasisAmount differs from SplitSum : SPLIT={G.R2(totalPay)} : DCBA={G.R2(deductionCalcBasisAmount)}");
                G.AddNoteToDataRow(pers, $"DeductionCalcBasisAmount differs from SplitSum : SPLIT={G.R2(totalPay)} : DCBA={G.R2(deductionCalcBasisAmount)}");
            }

            //
            // **** Split the Employee Contribution amount by work-day ratio and any rate difference between earning periods.
            //

            decimal currentEmployeeContribution = G.Decimal(pers, "CurrentAmountEmployee");
            decimal split1EmployeeRate = G.Decimal(pers, "Split1EmployeeRate");
            decimal split2EmployeeRate = G.Decimal(pers, "Split2EmployeeRate");
            decimal split1EmployeeContribution;
            decimal split2EmployeeContribution;
            decimal derivedEmployeeContribution;

            if ((G.HasMinimalDifference(split1EmployeeRate, split2EmployeeRate) == true) || (G.Contains(dbc, "P3")))
            {
                // When there is no rate-change between earning periods OR the employee is a PERS3 participant, then simply split the contribution amount by the hours ratio.
                split1EmployeeContribution = currentEmployeeContribution * split1HoursRatio;
                split2EmployeeContribution = currentEmployeeContribution * split2HoursRatio;
                derivedEmployeeContribution = split1EmployeeContribution + split2EmployeeContribution;
                if (G.HasMinimalDifference(currentEmployeeContribution, derivedEmployeeContribution) == false)
                {
                    G.DisplayWarning($"{employeeNumber} : ERROR : Employee Contribution differs on Split : SPLIT={G.R2(derivedEmployeeContribution)} : ORIG={G.R2(currentEmployeeContribution)}");
                    G.AddNoteToDataRow(pers, $"ERROR : Employee Contribution differs on Split : SPLIT={G.R2(derivedEmployeeContribution)} : ORIG={G.R2(currentEmployeeContribution)}");
                }
            }
            else
            {
                // When there is a rate change between earning periods, then calculate the new contribution amounts and log any difference between contribution totals.
                split1EmployeeContribution = split1EmployeeRate * (deductionCalcBasisAmount * split1HoursRatio);
                split2EmployeeContribution = split2EmployeeRate * (deductionCalcBasisAmount * split2HoursRatio);
                derivedEmployeeContribution = split1EmployeeContribution + split2EmployeeContribution;
                if (G.HasMinimalDifference(currentEmployeeContribution, derivedEmployeeContribution) == false)
                {
                    pers.BeginEdit();
                        pers["EmployeeContributionDifference"] = -(currentEmployeeContribution - derivedEmployeeContribution);
                    pers.EndEdit();
                    //G.AddNoteToDataRow(pers, $"RateChange : Employee Contribution imbalance : SPLIT={G.R2(derivedEmployeeContribution)} : ORIG={G.R2(currentEmployeeContribution)}");
                }
            }

            //
            // **** Split the Company contribution amount by the work-day ratio and/or any difference in rates per split-period.
            //

            decimal currentCompanyContribution = G.Decimal(pers, "CurrentAmountEmployer");
            decimal split1CompanyRate = G.Decimal(pers, "Split1CompanyRate");
            decimal split2CompanyRate = G.Decimal(pers, "Split2CompanyRate");
            decimal split1CompanyContribution;
            decimal split2CompanyContribution;
            decimal derivedCompanyContribution;

            if ((G.HasMinimalDifference(split1CompanyRate, split2CompanyRate) == true))
            {
                // When there is no rate-change between earning periods, then simply split the benefit amount by the hours ratio.
                split1CompanyContribution = currentCompanyContribution * split1HoursRatio;
                split2CompanyContribution = currentCompanyContribution * split2HoursRatio;
                derivedCompanyContribution = split1CompanyContribution + split2CompanyContribution;
                if (G.HasMinimalDifference(currentCompanyContribution, derivedCompanyContribution) == false)
                {
                    G.DisplayWarning($"{employeeNumber} : ERROR : Company Contribution differs on Split : SPLIT={G.R2(derivedCompanyContribution)} : ORIG={G.R2(currentCompanyContribution)}");
                    G.AddNoteToDataRow(pers, $"ERROR : Company Contribution differs on Split : SPLIT={G.R2(derivedCompanyContribution)} : ORIG={G.R2(currentCompanyContribution)}");
                }
            }
            else
            {
                // When there is a rate change between earning periods, then calculate the new contribution amounts and log any difference between contribution totals.
                split1CompanyContribution = split1CompanyRate * (deductionCalcBasisAmount * split1HoursRatio);
                split2CompanyContribution = split2CompanyRate * (deductionCalcBasisAmount * split2HoursRatio);
                derivedCompanyContribution = split1CompanyContribution + split2CompanyContribution;
                if (G.HasMinimalDifference(currentCompanyContribution, derivedCompanyContribution) == false)
                {
                    pers.BeginEdit();
                    pers["CompanyContributionDifference"] = -(currentCompanyContribution - derivedCompanyContribution);
                    pers.EndEdit();
                    //G.AddNoteToDataRow(pers, $"RateChange : Company Contribution imbalance : SPLIT={G.R2(derivedCompanyContribution)} : ORIG={G.R2(currentCompanyContribution)}");
                }
            }

            //
            // Update the PeriodControlDate Record for this employee.
            // 

            pers.BeginEdit();
                pers["ChargeDateTotalHours"] = totalHoursChgDt;
                pers["ChargeDateTotalPay"] = G.Decimal(pers, "Split1Pay") + G.Decimal(pers, "Split2Pay");
                //
                pers["Split1Pay"] = split1Pay;
                pers["Split1EmployeeContribution"] = split1EmployeeContribution;
                pers["Split1CompanyContribution"] = split1CompanyContribution;
                //
                pers["Split2Pay"] = split2Pay;
                pers["Split2EmployeeContribution"] = split2EmployeeContribution;
                pers["Split2CompanyContribution"] = split2CompanyContribution;
            pers.EndEdit();
            return true;
        }


        private bool Validate_COMMISSIONER_HoursAndPayTotals(string employeeNumber, DataRow pers)
        {
            // Process the same as a Non-Exempt employee.
            return Validate_NONEXEMPT_HoursAndPayTotals(employeeNumber, pers);
        }


        private bool Validate_RETIREE_HoursAndPayTotals(string employeeNumber, DataRow pers)
        {
            // Process similar to NonExempt or Exempt per EmployeeTypeCode status.
            if (IsNonExempt(pers))
                return Validate_NONEXEMPT_HoursAndPayTotals(employeeNumber, pers, true);
            else
                return Validate_EXEMPT_HoursAndPayTotals(employeeNumber, pers, true);
        }


        /// <summary>
        /// ProcessContiguousPeriodChargeDateRecords : Use this function to process ChargeDate records on a non-splitting pay period.
        /// </summary>
        /// <param name="inputRecords"></param>
        /// <param name="outputRecords"></param>
        /// <returns></returns>
        public bool ProcessContiguousPeriodChargeDateRecords(ref DataSet inputRecords, ref DataSet outputRecords)
        {
            if ((inputRecords == null) ||
                (outputRecords == null) ||
                (inputRecords.Tables.Contains(PersByPeriodControlDate.TableName) == false) ||
                (inputRecords.Tables.Contains(PersByChargeDates.TableName) == false))
                throw new Exception($"PersRecordParser::ProcessContiguousPeriodChargeDateRecords : null input arguments.");

            // Establish ReportPeriod and ReportNumber fields for each Split, prior and current. 
            _CurrentReportPeriod = _PersConfiguration.ReportPeriod();
            _CurrentReportNumber = _PersConfiguration.MonthlyReportNumber("G");
            _CurrentEarningPeriod = _PersConfiguration.EarningPeriod();

            // Parse pay-period ChargeDate records in order to update custom schema columns within each employeee PersByPeriodControlDate record.
            foreach (DataRow pers in inputRecords.Tables[PersByPeriodControlDate.TableName].AsEnumerable())
            {
                string employeeNumber = G.Field(pers, "EmployeeNumber").Trim();
                string dbc = G.Field(pers, "DeductionBenefitCode").Trim();
                decimal totalHours = 0.0m;
                decimal totalPay = 0.0m;

                // Get specified employee records from the ChargeDateRecords but only those that are PERS plan accountable by employee plan participation.
                IEnumerable<DataRow> empChargeRecs = null;

                if (G.IsSame(dbc, "PERS2"))
                {
                    var queryP2 = from r in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                            where ((r.Field<string>("EmployeeNumber").Trim().Equals(employeeNumber)) &&
                                         (r.Field<string>("Pers2Accountable").Contains("Y")))
                                  select r;
                    empChargeRecs = queryP2.ToArray();
                }
                else if (G.Contains(dbc, "P3"))
                {
                    var queryP3 = from r in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                  where ((r.Field<string>("EmployeeNumber").Trim().Equals(employeeNumber)) &&
                                         (r.Field<string>("Pers3Accountable").Contains("Y")))
                                  select r;
                    empChargeRecs = queryP3.ToArray();
                }
                else if (G.IsSame(dbc, "PERS1"))
                {
                    var queryP1 = from r in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                  where ((r.Field<string>("EmployeeNumber").Trim().Equals(employeeNumber)) &&
                                         (r.Field<string>("Pers1Accountable").Contains("Y")))
                                  select r;
                    empChargeRecs = queryP1.ToArray();
                }
                else if (G.IsSame(dbc, "PERS0"))   // PERS retirees who are still working. 
                {
                    var queryP0 = from r in inputRecords.Tables[PersByChargeDates.TableName].AsEnumerable()
                                  where ((r.Field<string>("EmployeeNumber").Trim().Equals(employeeNumber)) &&
                                         (r.Field<string>("Pers1Accountable").Contains("Y")))
                                  select r;
                    empChargeRecs = queryP0.ToArray();
                }
                else
                {
                    G.DisplayWarning($"{employeeNumber} : Employee is missing PERS Plan Code : DeductionBenefitCode={dbc}");
                    G.AddNoteToDataRow(pers, $"Employee is missing PERS Plan Code : DeductionBenefitCode={dbc}");
                }

                // Accumulate only PERS "eligible" hours and pay per ChargeDate report; see the "PERS?Accountable" columns therein.   

                if (empChargeRecs.Count() > 0)     // ChargeDate records found for this employee.
                {
                    foreach (DataRow e in empChargeRecs)
                    {
                        if ((G.IsDiff(employeeNumber, G.Field(e, "EmployeeNumber")) == true))
                            throw new Exception($"PersByChargeDates::ProcessContiguousPeriodChargeDateRecords : Employee ID mismatch : {employeeNumber} : {G.Field(e, "EmployeeNumber")}");

                        Debug.Assert(G.IsSame(employeeNumber, G.Field(e, "EmployeeNumber").Trim()));

                        totalHours += G.Decimal(e, "CurrentHours");
                        totalPay += G.Decimal(e, "CurrentAmount");

                        // Check for any charge date that occurs past a TerminationDate; employee should receive no pay past this date.  
                        // This scenario is common on final cash-out, and requires no adjustment within a contiguous, non-splitting, Earning Period.
                        if (G.IsSame("T", G.Field(e, "EmployeeStatusCode")) == true)
                        {
                            DateTime terminationDate;
                            DateTime chargeDate;
                            if ((DateTime.TryParse(G.Field(e, "TerminationDate"), out terminationDate) == true) &&
                                (DateTime.TryParse(G.Field(e, "ChargeDate"), out chargeDate) == true))
                            {
                                if (chargeDate > terminationDate)
                                {
                                    //G.DisplayWarning($"{employeeNumber} : Pay received past Termination date : ChargeDate={chargeDate.ToShortDateString()} : TermindationDate={terminationDate.ToShortDateString()}");
                                    G.AddNoteToDataRow(pers, $"*** Pay received past Termination date. Ignored on single EarningPeriod. *** ");
                                }
                            }
                        }
                    }
                }

                pers.BeginEdit();
                    pers["ReportPeriod"] = _CurrentReportPeriod;
                    pers["ReportNumber"] = _CurrentReportNumber;
                    pers["EarningPeriod"] = _CurrentEarningPeriod;
                    pers["ChargeDateTotalHours"] = totalHours;
                    pers["ChargeDateTotalPay"] = totalPay;
                pers.EndEdit();

                // *** Validations and Exception Adjustments ***

                decimal deductionCalcBasisAmount = G.Decimal(pers, "DeductionCalcBasisAmount");

                if ((IsRetiree(pers) == true) && (deductionCalcBasisAmount == 0.0m))
                {
                    // A retiree is a Plan 0 member. Someone who has once retired via PERS and later returned to work.
                    // Hence, there is no corrresponding DeductionCalcBasisAmount.  Instead, report their TotalEarningsAmount.
                    pers.BeginEdit();
                    pers["DeductionCalcBasisAmount"] = G.Field(pers, "TotalEarningAmount");
                    pers.EndEdit();

                    decimal retireeContribution = G.Decimal(pers, "CurrentAmountEmployee");
                    decimal retireeBenefit = G.Decimal(pers, "CurrentAmountEmployer");
                    if (retireeContribution != 0.0m)
                    {
                        G.DisplayWarning($"{employeeNumber} : Retiree with non-zero Contribution : {G.R2(retireeContribution)} ");
                        G.AddNoteToDataRow(pers, $"Retiree with non-zero Contribution : {G.R2(retireeContribution)} ");
                    }
                    if (retireeBenefit != 0.0m)
                    {
                        G.DisplayWarning($"{employeeNumber} : Retiree with non-zero Benefit : {G.R2(retireeBenefit)} ");
                        G.AddNoteToDataRow(pers, $"Retiree with non-zero Benefit : {G.R2(retireeBenefit)} ");
                    }
                }

                // For a PERS3 New Hire with no corresponding employee deduction, use their Charge Date report pay totals as the DeductionCalcBasisAmount instead.
                if ((G.IsSame(dbc, "P3NH") == true) && (deductionCalcBasisAmount == 0.0m))
                {
                    pers.BeginEdit();
                        pers["DeductionCalcBasisAmount"] = G.R2(totalPay);
                    pers.EndEdit();
                    G.AddNoteToDataRow(pers, $"PERS3 New Hire : DeductionCalcBasisAmount={G.R2(totalPay)}");
                }
            }
            return true;
        }

    }
}
