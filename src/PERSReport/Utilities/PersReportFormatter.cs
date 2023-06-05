using Microsoft.Office.Interop.Excel;
using PERSReport.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PERSReport.Utilities
{
    class PersReportFormatter : IDisposable
    {
        private ExceptionLog _Log;
        private string _TimeTag;
        private ArrayList _FinalLineItems;            

        public PersReportFormatter()
        {
            _Log = null;
            _TimeTag = string.Empty;
            _FinalLineItems = null;
        }

        public PersReportFormatter(ref ExceptionLog log, string timeTag)
        {
            _Log = log;
            _TimeTag = timeTag;
            _FinalLineItems = null;
        }

        ~PersReportFormatter()
        {
            if (_FinalLineItems != null)
                _FinalLineItems.Clear();
        }

        public void Dispose()
        {
            if (_FinalLineItems != null)
                _FinalLineItems.Clear();
        }

        public bool FormatFinalReportLineItems(ref DataSet outputRecords, string reportPeriod, string monthlyReportNumber, bool correctionReportType=false)
        {
            Debug.Assert(outputRecords != null);

            if (_FinalLineItems == null)
                _FinalLineItems = new ArrayList();
            else
                _FinalLineItems.Clear();

            string reportType = "R";   // Regular
            if (correctionReportType == true)
                reportType = "C";

            // ******** Summary Record
            // Example:  S,8821  ,201911,R,02,02,+002875215.36,+000218214.68,+000369198.84,+000056978.7,797,+000000000.0
            //
            const string SUM = "S,8821  ,{0},{1},{2},{3},{4},{5},{6},{7},{8},+000000000.0";
            // {0} = ReportingPeriod (e.g. December 2019 is 201912)
            // {1} = ReportType (R or C)
            // {2} = MonthlyReportNumber (e.g. 01, 02, 03)
            // {3} = ExpectedMonthlyReports (e.g. 02, 03)
            // {4} = TotalCompensation (the grand total of member compensation for all plans on the report)
            // {5} = TotalEmployeeContributions (the grand total of member contributions for all plans)
            // {6} = TotalEmployerContributions (the grand total of employer contributions for all plans)
            // {7} = TotalHours (the grand total of hours for all plans reported on the Defined Benefit Record)
            // {8} = TotalRecords (the total number of line-items following - not counting SummaryRecord nor any 'L' records)

            foreach (DataRow sum in outputRecords.Tables["SummaryRecord"].Rows)
            {
                decimal totalCompensation = G.Decimal(sum, "TotalCompensation");
                decimal totalEmployeeAmt = G.Decimal(sum, "TotalEmployeeAmt");
                decimal totalEmployerAmt = G.Decimal(sum, "TotalEmployerAmt");
                decimal totalHours = G.Decimal(sum, "TotalHours");
                //Int32 totalRecords = Int32.Parse(Globals.GetField(sum, "TotalRecords"));

                _FinalLineItems.Add(string.Format(SUM, G.Field(sum, "ReportPeriod"),
                                                       reportType,
                                                       G.Field(sum, "ReportNumber"),
                                                       G.Field(sum, "ReportCount"),
                                                       (totalCompensation >= 0.0m ? "+" : "-") + G.AR2(totalCompensation).PadLeft(12, '0'),
                                                       (totalEmployeeAmt >= 0.0m ? "+" : "-") + G.AR2(totalEmployeeAmt).PadLeft(12, '0'),
                                                       (totalEmployerAmt >= 0.0m ? "+" : "-") + G.AR2(totalEmployerAmt).PadLeft(12, '0'),
                                                       (totalHours >= 0.0m ? "+" : "-") + G.AR1(totalHours).PadLeft(11, '0'),
                                                       G.Field(sum, "TotalRecords")));
            }


            // ******** MBR Records : TODO : We currently make member profile changes using the DRS web-site directly.

            // ******** DBR Records
            foreach (DataRow dbr in outputRecords.Tables["DefinedBenefitRecords"].Rows)
            {
                /*  Example target DBR records format...
                B,8821  ,201911,R,02,123456789,P,2,05,201911, ,+112.0,+00.0,+0006043.35,+0000777.17,+0000477.42,A
                B,8821  ,201911,R,02,123456789,P,2,05,201911, ,+080.0,+00.0,+0005161.40,+0000663.76,+0000407.75,A
                B,8821  ,201911,R,02,123456789,P,2,05,201911, ,+080.0,+00.0,+0004923.80,+0000633.20,+0000388.98,A
                B,8821  ,201911,R,02,123456789,P,3,05,201911, ,+080.0,+00.0,+0009157.60,+0001177.67,+0000000.00,A
                B,8821  ,201911,R,02,123456789,P,3,05,201911, ,+101.0,+00.0,+0010597.14,+0001362.79,+0000000.00,A
                */
                const string DBR = "B,8821  ,{0},{1},{2},{3},P,{4},{5},{6}, ,{7},+00.0,{8}{9},{10}{11},{12}{13},{14}   ";
                // {0} = ReportingPeriod (e.g. December 2019 is 201912)
                // {1} = reportType (e.g. R or C)
                // {2} = MonthlyReportNumber, e.g. 02
                // {3} = SSN
                // {4} = PlanCode (e.g. 2 is PERS Plan2; 3 is PERS Plan3)
                // {5} = TypeCode - PERS Classification (e.g. 05, 13, 98, 99)
                // {6} = Earning Period (e.g. December 2019 is 201912)
                // {7} = Hours (e.g. +110.0) ; followed by Days worked (always reported as +00.0)
                // {8} + or -
                // {9} = Compensation - TotalEarningAmount (e.g. +0006043.35)
                // {10} + or -
                // {11} = EmployerAmt (e.g. +000777.17)
                // {12} + or -
                // {13} = EmployeeAmt (e.g. +477.42)
                // {14} = Status (e.g "A   ")

                decimal hours = G.Decimal(dbr, "Hours");
                decimal compensation = G.Decimal(dbr, "Compensation");
                decimal employeeAmt = G.Decimal(dbr, "EmployeeAmt");
                decimal employerAmt = G.Decimal(dbr, "EmployerAmt");

                _FinalLineItems.Add(string.Format(DBR, G.Field(dbr, "ReportPeriod"),
                                                       reportType,
                                                       G.Field(dbr, "ReportNumber").PadLeft(2, '0'),
                                                       G.Field(dbr, "SSN"),
                                                       G.Field(dbr, "PlanCode"),
                                                       G.Field(dbr, "TypeCode"),
                                                       G.Field(dbr, "EarningPeriod"),
                                                       (hours >= 0.0m ? "+" : "-") + G.AR1(hours).PadLeft(5, '0'),
                                                       (compensation >= 0.0m ? "+" : "-"), G.AR2(compensation).PadLeft(10, '0'),
                                                       (employerAmt >= 0.0m ? "+" : "-"), G.AR2(employerAmt).PadLeft(10, '0'),
                                                       (employeeAmt >= 0.0m ? "+" : "-"), G.AR2(employeeAmt).PadLeft(10, '0'),
                                                       G.Field(dbr, "Status")));
            }

            // ******* DCR Records
            foreach (DataRow dcr in outputRecords.Tables["DefinedContributionRecords"].Rows)
            {
                /*  Example target DCR records format (PERS 3 only)...
                 C,8821  ,201911,R,02,123456789,P,+0000357.19, ,SELF,C
                 C,8821  ,201911,R,02,123456789,P,+0000178.42, ,WSIB,A
                 C,8821  ,201911,R,02,123456789,P,+0000221.56, ,WSIB,A
                 C,8821  ,201911,R,02,123456789,P,+0000238.20, ,SELF,A
                 */
                const string DCR = "C,8821  ,{0},{1},{2},{3},P,{4}{5}, ,{6},{7}";
                // {0} = ReportingPeriod (e.g. December 2019 is 201912)
                // {1} = ReportType (R or C)
                // {2} = MonthlyReportNumber, e.g. 02
                // {3} = SSN
                // {4} + or -
                // {5} = Contribution Amount
                // {6} = Investment Program
                // {7} = Rate Option
                
                Decimal employeeAmt = G.Decimal(dcr, "EmployeeAmt");

                _FinalLineItems.Add(string.Format(DCR, G.Field(dcr, "ReportPeriod"),
                                                       reportType,
                                                       G.Field(dcr, "ReportNumber").PadLeft(2, '0'),
                                                       G.Field(dcr, "SSN"),
                                                       (employeeAmt >= 0.0m ? "+" : "-"), G.AR2(employeeAmt).PadLeft(10, '0'),
                                                       G.Field(dcr, "InvestProgram"),
                                                       G.Field(dcr, "RateOption")));
            }

            return true;
        }


        public bool WritePersReportFile(string filePath)
        {
            Debug.Assert(!string.IsNullOrEmpty(filePath));
            Debug.WriteLine($"Exporting final report to file: {filePath}");

            if ((_FinalLineItems == null) || (_FinalLineItems.Count == 0))
                throw new Exception($"PersReportFormattter::WritePersReportFile : Line item count is zero.");

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (string s in _FinalLineItems)
                {
                    writer.WriteLine(s);
                }
            }
            return true;
        }


        public bool WritePersValidationFile(ref DataSet outputRecords, ref ExcelIntegration excel, string filePath)
        {
            Debug.Assert(outputRecords != null);
            Debug.Assert(excel != null);
            Debug.Assert(!string.IsNullOrEmpty(filePath));
            Debug.WriteLine($"Exporting validation records to Excel file: {filePath}");

            if ((outputRecords == null) || (excel == null) || (string.IsNullOrEmpty(filePath)))
                throw new Exception($"PersReportFormattter::WritePersValidationFile : null argument exception.");

            excel.SaveDataSetToExcelFile(outputRecords, filePath);
            return true;
        }

    }
}
