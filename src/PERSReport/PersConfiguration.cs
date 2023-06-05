using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PERSReport.Utilities;

namespace PERSReport
{
    //
    //  Plan and Rates:  https://www.drs.wa.gov/employer/era-handbook/chapter-6/chapter-6-tables/
    //

    public record EmployerRate(string Plan, DateTime EffectiveDate, DateTime? EndDate, decimal Rate);

    public record MemberRate(string Plan, DateTime EffectiveDate, DateTime? EndDate, decimal Rate);

    public record PayPeriod(DateTime BeginDate, DateTime EndDate, DateTime CheckDate, DateTime? SplitStartDate, int MonthlyReportNumber, int MonthlyNumberOfReports);


    public class PersConfiguration
    {
        List<MemberRate>     MemberRates;
        List<EmployerRate>   EmployerRates;
        List<PayPeriod>      PayPeriods;
        PayPeriod            _SelectedPayPeriod;
        bool                 _UsePriorMonthAsReportingPeriod;

        // Parameters that may be overridden from via command line options
        private Int32 _MonthlyReportNumber { get; set; }             // From a DRS perspective, this is report number X of Y  
        private Int32 _ExpectedNumberOfMonthlyReports { get; set; }  // From a DRS perspective, there are Y expected reports this month

        public PersConfiguration()
        {
            MemberRates = null;
            EmployerRates = null;
            PayPeriods = null;
            _SelectedPayPeriod = null;
            _MonthlyReportNumber = 0;
            _ExpectedNumberOfMonthlyReports = 0;
            _UsePriorMonthAsReportingPeriod = false;
        }

        public PersConfiguration(IList<JToken> memberRates, IList<JToken> employerRates, IList<JToken> payPeriods, bool usePriorMonthAsReportingPeriod = false)
        {
            //
            // Input is parsed from file PersConfiguration.json
            //
            Debug.Assert((memberRates != null) && (employerRates != null) && (payPeriods != null));

            MemberRates = new List<MemberRate>();
            foreach(JToken mr in memberRates)
            {
                MemberRate mbr = mr.ToObject<MemberRate>();
                MemberRates.Add(mbr);
            }

            EmployerRates = new List<EmployerRate>();
            foreach (JToken er in employerRates)
            {
                EmployerRate emr = er.ToObject<EmployerRate>();
                EmployerRates.Add(emr);
            }

            PayPeriods = new List<PayPeriod>();
            foreach (JToken pp in payPeriods)
            {
                PayPeriod pay = pp.ToObject<PayPeriod>();
                PayPeriods.Add(pay);
            }

            _UsePriorMonthAsReportingPeriod = usePriorMonthAsReportingPeriod;

            // TODO : Validation of input configuration values

            return;
        }


        public PayPeriod GetSelectedPayPeriodRecord()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            return _SelectedPayPeriod;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nearCheckDate"></param>
        /// <param name="monthlyReportNumber">If non-zero, then this value will override that from the selected pay period</param>
        /// <param name="expectedNumberOfMonthlyReports">If non-zero, then this value will override that from the selected pay period</param>
        /// <param name="payPeriod"></param>
        /// <returns></returns>
        public bool SelectPayPeriodRecord(DateTime nearCheckDate, out PayPeriod payPeriod)
        {
            Debug.Assert(PayPeriods != null);
            payPeriod = null;

            if ((PayPeriods != null) && (PayPeriods.Count > 0))
            {
                IEnumerable<PayPeriod> periods = from pp in PayPeriods
                                                 where (pp != null) && ((nearCheckDate == pp.CheckDate) || (Math.Abs((nearCheckDate - pp.CheckDate).TotalDays) <= 7))
                                                 select pp;
                List<PayPeriod> results = periods.ToList<PayPeriod>();
                if (results.Count > 0)
                {
                    Debug.Assert(results.Count == 1);
                    payPeriod = results[0];
                    _SelectedPayPeriod = results[0];
                    return true;
                }
            }

            return false;
        }

        public bool OverrideConfiguredMonthlyReportNumber(Int32 monthlyReportNumber)
        {
            //
            // The Payroll Team may choose to set the ReportNumber via command-line parameter instead of from PersConfiguration.json file.
            //
            if ((monthlyReportNumber >= 1) && (monthlyReportNumber <= 3))
            {
                _MonthlyReportNumber = monthlyReportNumber;
                return true;
            }
            return false;      
        }

        public bool OverrideConfiguredExpectedNumberOfMonthlyReports(Int32 expectedNumberOfMonthlyReports)
        {
            //
            // The Payroll Team may choose to set the ExpectedNumberOfMonthlyReports via command-line parameter instead of from PersConfiguration.json file.
            //
            if ((expectedNumberOfMonthlyReports >= 1) && (expectedNumberOfMonthlyReports <= 3))
            {
                _ExpectedNumberOfMonthlyReports = expectedNumberOfMonthlyReports;
                return true;
            }
            return false;
        }


        public decimal Month1EmployerContributionRate(string planType) 
        { Debug.Assert(_SelectedPayPeriod != null);  return GetEmployerContributionRateForYearAndMonth(_SelectedPayPeriod.BeginDate, planType); }

        public decimal Month2EmployerContributionRate(string planType) 
        { Debug.Assert(_SelectedPayPeriod != null); return GetEmployerContributionRateForYearAndMonth(_SelectedPayPeriod.EndDate, planType); }

        public decimal GetEmployerContributionRateForYearAndMonth(DateTime date, string persPlanTypeCode)
        {
            Debug.Assert((EmployerRates != null) && (!string.IsNullOrEmpty(persPlanTypeCode)));
            Debug.Assert((string.Compare(persPlanTypeCode, @"PERS1", true) == 0) || (string.Compare(persPlanTypeCode, @"PERS2", true) == 0) || (persPlanTypeCode.Contains("P3") == true) || (string.Compare(persPlanTypeCode, @"PERS0", true) == 0));

            string plan = persPlanTypeCode;
            if (persPlanTypeCode.Contains("P3") == true)    // Note: PERS3 employer contributions match those of PERS2 plan.
                plan = @"PERS3";

            if ((EmployerRates != null) && (EmployerRates.Count > 0))
            {
                IEnumerable<EmployerRate> rates = from er in EmployerRates
                                                  where (er != null) 
                                                        && (string.Equals(er.Plan, plan, StringComparison.OrdinalIgnoreCase))
                                                        && ((date >= er.EffectiveDate))
                                                        && ((er.EndDate == null) || ((date <= er.EndDate)))
                                                  select er;
                List<EmployerRate> results = rates.ToList<EmployerRate>();
                if (results.Count > 0)
                    return results[0].Rate;
            }

            // default to return value that should be observable as an error condition.
            return 0.0m;
        }


        public decimal Month1MemberContributionRate(string planType)
        { Debug.Assert(_SelectedPayPeriod != null); return GetMemberContributionRateForYearAndMonth(_SelectedPayPeriod.BeginDate, planType); }

        public decimal Month2MemberContributionRate(string planType)
        { Debug.Assert(_SelectedPayPeriod != null); return GetMemberContributionRateForYearAndMonth(_SelectedPayPeriod.EndDate, planType); }


        public decimal GetMemberContributionRateForYearAndMonth(DateTime date, string persPlanTypeCode)
        {
            Debug.Assert((MemberRates != null) && (!string.IsNullOrEmpty(persPlanTypeCode)));
            Debug.Assert((string.Compare(persPlanTypeCode, @"PERS1", true) == 0) || (string.Compare(persPlanTypeCode, @"PERS2", true) == 0) || (persPlanTypeCode.Contains("P3") == true) || (string.Compare(persPlanTypeCode, @"PERS0", true) == 0));

            if (persPlanTypeCode.Contains("P3") == true)   // Return 0.0 for PERS3 member rates as these are set by percent varying with member-age and Option code.
                return 0.0m;                               // PERS3 member contributions are rated on plan option indicators: (A, B, C, D, E, F).
                                                           // See: https://www.drs.wa.gov/employer/era-handbook/chapter-6/chapter-6-tables/

            if ((MemberRates != null) && (MemberRates.Count > 0))
            {
                IEnumerable<MemberRate> rates = from mr in MemberRates
                                                where (mr != null) 
                                                      && (string.Equals(mr.Plan, persPlanTypeCode, StringComparison.OrdinalIgnoreCase))
                                                      && (date >= mr.EffectiveDate)
                                                      && ((mr.EndDate == null) || (date <= mr.EndDate))
                                                select mr;
                List < MemberRate > results = rates.ToList<MemberRate>();
                if (results.Count > 0)
                    return results[0].Rate;
            }

            // default to return value that should be observable as an error condition.
            return 0.0m;
        }

        
        public bool IsSplitPeriodWithRateChanges()
        {
            //
            //  Assume that any DRS rate change will always effect the PERS2 plan - i.e. don't check for PERS1 or PERS3 only changes...
            //
            Debug.Assert(_SelectedPayPeriod != null);
            if (IsThisASplitMonthPayPeriod())
            {
                return !(G.HasMinimalDifference(Month1MemberContributionRate("PERS2"), Month2MemberContributionRate("PERS2")));
            }
            return false;
        }


        public DateTime PeriodControlDate()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            return _SelectedPayPeriod.CheckDate;
        }

        public string PeriodControlDay()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            return _SelectedPayPeriod.CheckDate.ToString("yyyyMMdd");
        }

        public DateTime PayPeriodStartDate()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            return _SelectedPayPeriod.BeginDate;
        }


        public DateTime? PayPeriodSplitDate()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            return _SelectedPayPeriod.SplitStartDate;
        }


        public DateTime PayPeriodEndDate()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            return _SelectedPayPeriod.EndDate;
        }


        public string ReportPeriod()
        {
            Debug.Assert(_SelectedPayPeriod != null);

            if (_UsePriorMonthAsReportingPeriod == true)
                return string.Format("{0}{1:00}", _SelectedPayPeriod.BeginDate.Year, _SelectedPayPeriod.BeginDate.Month);

            return string.Format("{0}{1:00}", _SelectedPayPeriod.EndDate.Year, _SelectedPayPeriod.EndDate.Month);
        }


        public Int32 MonthlyReportNumber()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            if (_MonthlyReportNumber != 0)
                return _MonthlyReportNumber;
            return _SelectedPayPeriod.MonthlyReportNumber;
        }

        public string MonthlyReportNumber(string formatSpecifer = "G")
        {
            Debug.Assert(_SelectedPayPeriod != null);
            if (_MonthlyReportNumber != 0)
                return string.Format("0{0:0}", _MonthlyReportNumber);
            return string.Format("0{0:0}", _SelectedPayPeriod.MonthlyReportNumber);
        }


        public Int32 ExpectedNumberOfMonthlyReports()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            if (_ExpectedNumberOfMonthlyReports != 0)
                return _ExpectedNumberOfMonthlyReports;
            return _SelectedPayPeriod.MonthlyNumberOfReports;
        }

        public string ExpectedNumberOfMonthlyReports(string formatSpecifer = "G")
        {
            Debug.Assert(_SelectedPayPeriod != null);
            if (_ExpectedNumberOfMonthlyReports != 0)
                return string.Format("0{0:0}", _ExpectedNumberOfMonthlyReports);
            return string.Format("0{0:0}", _SelectedPayPeriod.MonthlyNumberOfReports);
        }


        public string EarningPeriod()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            return string.Format("{0}{1:00}", _SelectedPayPeriod.EndDate.Year, _SelectedPayPeriod.EndDate.Month);
        }

        public string PriorEarningPeriod()
        {
            Debug.Assert(_SelectedPayPeriod != null);
            return string.Format("{0}{1:00}", _SelectedPayPeriod.BeginDate.Year, _SelectedPayPeriod.BeginDate.Month);
        }


        public bool IsThisASplitMonthPayPeriod()
        {
            Debug.Assert(_SelectedPayPeriod != null); 
            return (_SelectedPayPeriod.BeginDate.Month != _SelectedPayPeriod.EndDate.Month);
        }


        private decimal _month1WorkDayRatio = 0.0m;
        private decimal _month2WorkDayRatio = 0.0m;
        //
        public decimal Month1WorkDayRatio() 
        { Debug.Assert(_SelectedPayPeriod != null); return _month1WorkDayRatio; }
        //
        public decimal Month2WorkDayRatio() 
        { Debug.Assert(_SelectedPayPeriod != null); return _month2WorkDayRatio; }
        //
        public bool GetWorkWeekDaysInSelectedPayPeriodByMonth(out int daysInMonth1, out int daysInMonth2)
        {
            Debug.Assert(_SelectedPayPeriod != null);

            daysInMonth1 = 0;
            daysInMonth2 = 0;

            if (IsThisASplitMonthPayPeriod() == true)
            {
                // count the actual Monday thru Friday days within each period segment.
                for (int days = 0; days < ((DateTime.DaysInMonth(_SelectedPayPeriod.BeginDate.Year, _SelectedPayPeriod.BeginDate.Month) - _SelectedPayPeriod.BeginDate.Day) + 1); days++)
                {
                    DateTime day = _SelectedPayPeriod.BeginDate.AddDays(days);
                    if ((day.DayOfWeek >= DayOfWeek.Monday) && (day.DayOfWeek <= DayOfWeek.Friday))
                        daysInMonth1++;
                }

                for (int days = 0; days < _SelectedPayPeriod.EndDate.Day; days++)
                {
                    DateTime day = _SelectedPayPeriod.EndDate.AddDays(-days);
                    if ((day.DayOfWeek >= DayOfWeek.Monday) && (day.DayOfWeek <= DayOfWeek.Friday))
                        daysInMonth2++;
                }
                Debug.Assert(daysInMonth1 + daysInMonth2 == 10);

                _month1WorkDayRatio = daysInMonth1 * 8.0m / 80.0m;
                _month2WorkDayRatio = daysInMonth2 * 8.0m / 80.0m;

                return true;
            }

            // Else, 10 work-week days in contiguous 2 week period.
            Debug.Assert(((_SelectedPayPeriod.EndDate.Day - _SelectedPayPeriod.BeginDate.Day) - 4) == 10);
            daysInMonth1 = 10;
            return true;
        }

    }
}
