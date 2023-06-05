using PERSReport.Models;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using System;
using System.Globalization;

namespace PERSReport.Utilities
{
    /// <summary>
    /// PersRecordParser : Account for split-month pay-period in preparation for output records parsing.
    /// </summary>
    class PersRecordParser 
    {
        private ExceptionLog _Log;
        private string _TimeTag;
        PersByChargeDates _ChargeDates;
        PersByPeriodControlDate _PeriodControlDates;
        PersConfiguration _PersConfiguration;
 
        public PersRecordParser()
        {
            _Log = null;
            _TimeTag = string.Empty;
            _PersConfiguration = null;
            _ChargeDates = null;
            _PeriodControlDates = null;
        }

        public PersRecordParser(ref PersConfiguration persConfiguration,
                                ref PersByChargeDates persByChargeDates,
                                ref PersByPeriodControlDate persByPeriodControlDate,
                                ref ExceptionLog log, 
                                string timeTag)
        {
            Debug.Assert(persConfiguration is PersConfiguration);
            Debug.Assert(persByChargeDates is PersByChargeDates);
            Debug.Assert(persByPeriodControlDate is PersByPeriodControlDate);
            Debug.Assert(log is ExceptionLog);

            _PersConfiguration = persConfiguration;
            _ChargeDates = persByChargeDates;
            _PeriodControlDates = persByPeriodControlDate;
            _Log = log;
            _TimeTag = timeTag;
        }


        ~PersRecordParser()
        {
            _Log = null;
            _TimeTag = string.Empty;
            _ChargeDates = null;
            _PeriodControlDates = null;
            _PersConfiguration = null;
        }


        private bool StageOutputRecordsSchema(ref DataSet outputRecords, bool IsSplitMonthPeriod=false)
        {
            if (outputRecords == null)
                throw new Exception($"PersRecordParser::StageOutputRecordsSchema : null argument.");

            if ((outputRecords.Tables.Contains("DefinedBenefitRecords") == true) &&
                (outputRecords.Tables.Contains("DefinedContributionRecords") == true) &&
                (outputRecords.Tables.Contains("ManualVerifyRecords") == true) &&
                (outputRecords.Tables.Contains("SummaryRecord") == true) &&
                (outputRecords.Tables.Contains("DRSInvoice") == true))
                return true;

            // **** Be careful about the order and names of fields within these "Staging Tables".  Subsquent "Row.Add" statements herein depend upon correct and matching order.
            // **** Note that all output record fields are type String by default. Input fields may be typed differently.

            // Staging table for DBR records.
            outputRecords.Tables.Add("DefinedBenefitRecords");
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("ReportPeriod");
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("ReportNumber");
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("EmployeeNum");
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("LastName");
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("SSN");
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("PlanCode");           // DeductionBenefitCode e.g. PERS0, PERS1, PERS2, PERS3, P3BW3, etc.
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("TypeCode");           // PersClassification e.g. 05, 13, 98, 99   
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("EarningPeriod");      // not necessarily the same as ReportPeriod (e.g. would be different in case of Correction type reports)
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("Hours", typeof(decimal));
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("Compensation", typeof(decimal));
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("EmployerAmt", typeof(decimal));
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("EmployeeAmt", typeof(decimal));
            outputRecords.Tables["DefinedBenefitRecords"].Columns.Add("Status");


            // Staging table for DCR records; PERS 3 contributions
            outputRecords.Tables.Add("DefinedContributionRecords");
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("ReportPeriod");
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("ReportNumber");
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("EmployeeNum");
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("LastName");
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("SSN");
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("PlanCode");        // DeductionBenefitCode e.g. P3BW3, etc.
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("EmployeeAmt", typeof(decimal));
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("InvestProgram");   // Deduction e.g. PERS 3 Option A Self
            outputRecords.Tables["DefinedContributionRecords"].Columns.Add("RateOption");      // RateOption e.g. A, B, C, D, E, F

            // Copy of the PeriodControlDate table from InputRecords
            //if (IsSplitMonthPeriod == true)
            //{
            //    outputRecords.Tables.Add("SplitPeriodCalculations");
            //}

            // Staging table for non-classified records; e.g. blank Deduction codes, or M and C type paycheck deductions. 
            outputRecords.Tables.Add("ManualVerifyRecords");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("EmployeeNum");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("LastName");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("SSN");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("PlanCode");        // DeductionBenefitCode e.g. PERS0, PERS1, PERS2, PERS3, P3BW3, etc.
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("TypeCode");        // PersClassification e.g. 05, 13, 98, 99   
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Hours");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Compensation");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("EmployerAmt");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("EmployeeAmt");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Status");
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("CheckAddMode");
            if (IsSplitMonthPeriod == true)
            {
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("ReportPeriod");   
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("ReportNumber");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("ChargeDateTotalHours");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("ChargeDateTotalPay");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split1Hours");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split2Hours");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split1HoursRatio");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split2HoursRatio");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split1EarningPeriod");     
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split2EarningPeriod");     
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split1Pay");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split2Pay");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split1CompanyRate");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split2CompanyRate");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split1EmployeeRate");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split2EmployeeRate");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split1CompanyContribution");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split2CompanyContribution");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split1EmployeeContribution");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Split2EmployeeContribution");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("CompanyContributionDifference");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("EmployeeContributionDifference");
            }
            else 
            {
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("ReportPeriod");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("ReportNumber");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("EarningPeriod");           
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("ChargeDateTotalHours");
                outputRecords.Tables["ManualVerifyRecords"].Columns.Add("ChargeDateTotalPay");
            }
            outputRecords.Tables["ManualVerifyRecords"].Columns.Add("Note");   // common field

            // **** Be careful about the order and names of fields within these "Staging Tables".  Subsquent "Row.Add" statements herein depend upon correct and matching order.

            // Staging table for DRS MRL Summary record.
            outputRecords.Tables.Add("SummaryRecord");
            outputRecords.Tables["SummaryRecord"].Columns.Add("ReportPeriod");
            outputRecords.Tables["SummaryRecord"].Columns.Add("ReportNumber");
            outputRecords.Tables["SummaryRecord"].Columns.Add("ReportCount");
            outputRecords.Tables["SummaryRecord"].Columns.Add("TotalCompensation");
            outputRecords.Tables["SummaryRecord"].Columns.Add("TotalEmployeeAmt");
            outputRecords.Tables["SummaryRecord"].Columns.Add("TotalEmployerAmt");
            outputRecords.Tables["SummaryRecord"].Columns.Add("TotalHours");
            outputRecords.Tables["SummaryRecord"].Columns.Add("TotalRecords");

            // Staging table for DRS Invoice record.
            outputRecords.Tables.Add("DRSInvoice");
            outputRecords.Tables["DRSInvoice"].Columns.Add("EmployerSharePlan1");
            outputRecords.Tables["DRSInvoice"].Columns.Add("EmployerSharePlan2");
            outputRecords.Tables["DRSInvoice"].Columns.Add("EmployerSharePlan3");
            outputRecords.Tables["DRSInvoice"].Columns.Add("TotalEmployerShare");
            //--
            outputRecords.Tables["DRSInvoice"].Columns.Add("EmployeeSharePlan1");
            outputRecords.Tables["DRSInvoice"].Columns.Add("EmployeeSharePlan2");
            outputRecords.Tables["DRSInvoice"].Columns.Add("EmployeeSharePlan3W");
            outputRecords.Tables["DRSInvoice"].Columns.Add("EmployeeSharePlan3S");
            outputRecords.Tables["DRSInvoice"].Columns.Add("TotalEmployeeShare");
            //--
            outputRecords.Tables["DRSInvoice"].Columns.Add("TotalPERSContribution");
            //
            outputRecords.Tables["DRSInvoice"].Columns.Add("EmployeeRegisterAdjustment");   // Total Employee Register Adjustment due to any DRS Rate Changes within a PayPeriod
            outputRecords.Tables["DRSInvoice"].Columns.Add("CompanyRegisterAdjustment");    // Total Company Register Adjustment due to any DRS Rate Changes within a PayPeriod

            return true;
        }


        private bool StageInputRecordsSchema(ref DataSet inputRecords, bool splitMonth=false)
        {
            if (inputRecords.Tables.Contains(PersByPeriodControlDate.TableName) == false)
                return false;

            // Extend the columns of the Input Records (i.e. PersByPeriodControlDate report) in order to incrementally tabulate 
            // Split-Month rates and derived values. Most of this data is derived during the PersByChargeDate processing.

            if (splitMonth == true)
            {
                // Add Split-Period schema elements to the inputRecords PersByPeriodControlDate Table in order to merge ChargeDate summaries from first/second pay-period records.
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("ReportPeriod");                
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("ReportNumber");
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("ChargeDateTotalHours", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("ChargeDateTotalPay", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split1Hours", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split2Hours", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split1HoursRatio", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split2HoursRatio", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split1EarningPeriod");          
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split2EarningPeriod");          
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split1Pay", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split2Pay", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split1CompanyRate", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split2CompanyRate", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split1EmployeeRate", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split2EmployeeRate", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split1CompanyContribution", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split2CompanyContribution", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split1EmployeeContribution", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Split2EmployeeContribution", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("CompanyContributionDifference", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("EmployeeContributionDifference", typeof(decimal));
            }
            else
            {
                // Add normal contiguous-period schema elements to the inputRecords PersByPeriodControlDate Table in order to merge ChargeDate summaries from pay-period records. 
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("ReportPeriod");
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("ReportNumber");
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("EarningPeriod");   
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("ChargeDateTotalHours", typeof(decimal));
                inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("ChargeDateTotalPay", typeof(decimal));
            }
            inputRecords.Tables[PersByPeriodControlDate.TableName].Columns.Add("Note");   // common field
            return true;
        }


        /// <summary>
        /// **** Parse Input Records : Determine if this is a split-month and prepare output records accordingly.
        /// </summary>
        /// <param name="inputRecords"></param>
        /// <param name="outputRecords"></param>
        /// <returns></returns>
        public bool ProcessInputRecords(ref DataSet inputRecords, ref DataSet outputRecords)
        {
            Debug.Assert(inputRecords != null);
            Debug.Assert(outputRecords != null);

            bool IsSplitMonthPayPeriod = _PersConfiguration.IsThisASplitMonthPayPeriod();

            // Extend the input records tables and corresponding schemas prior to deriving pay-period fields/values.
            if (StageInputRecordsSchema(ref inputRecords, IsSplitMonthPayPeriod) == false)
                throw (new Exception("PersRecordParser::ProcessInputRecords : Error configuring InputRecords schema."));

            // Prepare the output records tables and corresponding schemas prior to processing input records.
            if (StageOutputRecordsSchema(ref outputRecords, IsSplitMonthPayPeriod) == false)
                throw (new Exception("PersRecordParser::ProcessInputRecords : Error configuring OutputRecords schema."));

            // Parse records of corresponding ChargeDate Report; via Split-Month PayPeriod or Contiguous PayPeriod

            if (IsSplitMonthPayPeriod == true)
            {
                if (_ChargeDates.ProcessSplitMonthChargeDateRecords(ref inputRecords, ref outputRecords) == false)
                    throw (new Exception("PersRecordParser::ProcessInputRecords : Error processing split-month ChargeDate records."));
            }
            else
            {
                if (_ChargeDates.ProcessContiguousPeriodChargeDateRecords(ref inputRecords, ref outputRecords) == false)
                    throw (new Exception("PersRecordParser::ProcessInputRecords : Error processing ChargeDate records."));
            }

            // Create DBR and DCR records-sets for output processing.
            if (_PeriodControlDates.ParseContributionRecords(ref inputRecords, ref outputRecords, IsSplitMonthPayPeriod) == false)
                throw (new Exception("Error parsing contribution records."));

            // Capture a copy of the InputRecords PersByPeriodControlDate.TableName table into the OutputRecords dataset for full-scoped Payroll validation purposes.
            outputRecords.Tables.Add(inputRecords.Tables[PersByPeriodControlDate.TableName].Copy());

            // Create a Summary header record with subtotal accumulations.
            if (_PeriodControlDates.DeriveSummaryAndInvoiceRecords(ref outputRecords, _PersConfiguration.ReportPeriod(), _PersConfiguration.MonthlyReportNumber("G"), _PersConfiguration.ExpectedNumberOfMonthlyReports("G")) == false)
                throw (new Exception("Error parsing summary record."));

            return true;
        }

     }
}
