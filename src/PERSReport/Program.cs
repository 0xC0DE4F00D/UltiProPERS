using CommandLine;
using Newtonsoft.Json.Linq;
using PERSReport.Services;
using PERSReport.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*  
---------------------------------------------------------------------------------
--
-- VERSION 3.x, July, 2021
--
-- Phil@UsefulEngines.com
--
--	Washington State Department of Retirement Systems	Questions: Contact Employer Support Services at					
--	Transmittal Template Multiple Record Layout (MRL)	360/664-7200 or 1-800-547-6657 
--  				
--	Refer to The DRS Employer Handbook	http://www.wa.gov/DRS/employer/drsn/	
--
---------------------------------------------------------------------------------

Debugging command lines:  
    (1)     -a -d "20210408" -n 3 -e 3 -f "..\..\TEST\ppennington"
    (2)     -j "..\..\PersConfiguration.json" -f "..\..\TEST\ppennington" -a -d "20210617"                   
 */

// C# 9 with VS2019 Bug:  https://stackoverflow.com/questions/64749385/predefined-type-system-runtime-compilerservices-isexternalinit-is-not-defined
using System.ComponentModel;
namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit { }
}

namespace PERSReport
{
    class Options
    {
        [Option('j', "ConfigurationFile", Required = true, Default = null,
          HelpText = "Input JSON file. Automation configuration of Rates, Payroll Calendar, etc.")]
        public string ConfigurationFile { get; set; }

        [Option('a', "UseWebServices", Required = false, Default = false,
          HelpText = "Download Cognos reports via Web Services API. Default is true. Alternative is to provide input via Cognos files.")]
        public bool UseWebServices { get; set; }

        [Option('d', "PeriodControlDate", Required = false, Default = null,
          HelpText = "YYYYMMDD - UltiPro Payroll Period Control Date.")]
        public string PeriodControlDate { get; set; }

        [Option('n', "MonthlyReportNumber", Required = false, Default = null,
          HelpText = "DRS report number X of expected number Y by month (e.g. 1, 2, or 3)")]
        public string MonthlyReportNumber { get; set; }

        [Option('e', "ExpectedNumberOfMonthlyReports", Required = false, Default = null,
          HelpText = "Expected number Y of DRS monthly reports X (e.g. 2, or 3)")]
        public string ExpectedNumberOfMonthlyReports { get; set; }

        [Option('r', "ByPeriodReport", Required = false, Default = null,
          HelpText = "Optional input file. Cognos RunPERSbyPeriodControlDateV2 report exported as Excel Data file.")]
        public string ByPeriodReport { get; set; }

        [Option('s', "ByDailyHoursReport", Required = false, Default = null,
          HelpText = "Optional input file. Cognos RunPERSbyDailyHours report exported as Excel Data file.")]
        public string ByDailyHoursReport { get; set; }

        [Option('f', "OutputFolder", Required = true, Default = null,
          HelpText = "Output folder path. Folder to receive report output files.")]
        public string OutputFolder { get; set; }

        [Option('c', "FieldSeparator", Required = false, Default = ',',
          HelpText = "Optional .csv file field separation character. Default separator is the ',' character.")]
        public char FieldSeparator { get; set; }

        [Option('w', "WorksheetName", Required = false, Default = "page",
          HelpText = "Cognos RunPERS data worksheet name. Default is 'page'.")]
        public string WorksheetName { get; set; }

        [Option('x', "CreateCorrectionReport", Required = false, Default = false,
          HelpText = "Create a correction type report instead of a regular type.")]
        public bool CreateCorrectionReport { get; set; }

        [Option('z', "ForceCreateSinglePeriodReport", Required = false, Default = false,
          HelpText = "Create a single-period report even if a split-period report is recommended.")]
        public bool ForceCreateSinglePeriodReport { get; set; }

        [Option('p', "UsePriorMonthAsReportingPeriod", Required = false, Default = false,
          HelpText = "On a split-month pay period, use the prior month as the DRS Reporting period.")]
        public bool UsePriorMonthAsReportingPeriod { get; set; }
    }

    class MyProgram
    {
        private string _TimeTag;                // Establish a time-stamp.
        private string _LogFile;                // File to receive data-transformation exception data.
        public ExceptionLog _Log;               // Helper class.
        private string _OutputFolder;           // Path to file outputs.

        private string _MyConfigurationFile;                // JSON file with lists of configuration parameters.
        private PersConfiguration _MyPersConfiguration;     // JSON string from configuration file.

        private bool _UseWebServices;                    // Alternatively, use Cognos exported excel-data input files.
        private CognosReportAsAService _CognosRaaS;      // Cognos Report as a Service API.

        private ExcelIntegration _ExcelApp;

        private bool _UsePriorMonthAsReportingPeriod;   // Option to indicate use of prior month on split-period reporting (instead of current month).

        static void Main(string[] args)
        {
            MyProgram myApp = new MyProgram();
            myApp._Log = new ExceptionLog();
            myApp._TimeTag = DateTime.Now.ToString(@"yyyy'-'MM'-'dd'-'HHmm");
            myApp._CognosRaaS = null;
            myApp._ExcelApp = null;
            myApp._MyPersConfiguration = null;

            try
            {
                Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       if (!string.IsNullOrEmpty(o.ConfigurationFile))
                           myApp._MyConfigurationFile = Path.GetFullPath(o.ConfigurationFile);

                       myApp._UseWebServices = o.UseWebServices;

                       if (!string.IsNullOrEmpty(o.OutputFolder))
                           myApp._OutputFolder = Path.GetFullPath(o.OutputFolder);

                       myApp._UsePriorMonthAsReportingPeriod = o.UsePriorMonthAsReportingPeriod;
                   })
                   .WithNotParsed<Options>((errs) => HandleParseErrors(errs));

                G.Display($"*****************************************************************************************");
                G.Display($"myHR Department of Retirement Services MRL Report Generation {myApp._TimeTag}");
                G.Display($"*****************************************************************************************");


                //
                //  Configure Log file settings.
                //
                myApp._LogFile = myApp._OutputFolder + "\\" + "Logs" + "\\" + "Log-" + myApp._TimeTag + ".xml";

                //
                //  Import PersConfiguration.json file settings...
                //
                if (string.IsNullOrEmpty(myApp._MyConfigurationFile) == false)
                {
                    G.Display($"\nImporting Payroll Calendar and Contribution Rates configuration from file:\n\t {myApp._MyConfigurationFile}\n");

                    var jsonInput = File.ReadAllText(myApp._MyConfigurationFile);
                    JObject myJsonObj = JObject.Parse(jsonInput);
                    if (myJsonObj != null)
                    {
                        IList<JToken> memberRates = myJsonObj["PersConfiguration"]["MemberRates"].Children().ToList();
                        IList<JToken> employerRates = myJsonObj["PersConfiguration"]["EmployerRates"].Children().ToList();
                        IList<JToken> payPeriods = myJsonObj["PersConfiguration"]["PayPeriods"].Children().ToList();
                        myApp._MyPersConfiguration = new PersConfiguration(memberRates, employerRates, payPeriods, myApp._UsePriorMonthAsReportingPeriod);
                    }
                }
                else
                {
                    throw (new Exception("A PersConfiguration.json file is required. Further processing halted."));
                }

                //
                //  Configure Cognos ReportAsAService API connectivity.
                //
                if (myApp._UseWebServices)
                {
                    G.Display("Initializing connections with Cognos web services...\n");
                    myApp._CognosRaaS = new CognosReportAsAService(ref myApp._Log);

                    if (myApp._CognosRaaS.Initialize(PERSReport.Properties.Settings.Default.ClientAccessKey,
                                                     PERSReport.Properties.Settings.Default.UserName,
                                                     PERSReport.Properties.Settings.Default.Password,
                                                     PERSReport.Properties.Settings.Default.UserAccessKey) == false)
                        throw (new Exception("Failed to initialize connection with UltiPro API Services. Further processing halted."));
#if DEBUG
                    // myApp._CognosRaaS.TestRaaS();
#endif
                }

                //
                //  Configure Excel integration.
                //
                myApp._ExcelApp = new ExcelIntegration();

                //
                // Execute the bulk of PERS input and output processing via the MyBusinessLogic object.
                //

                MyBusinessLogic b = new(ref myApp._MyPersConfiguration, ref myApp._CognosRaaS, ref myApp._ExcelApp, ref myApp._Log, myApp._TimeTag);

                if (b.ParseArguments(args) == false)
                    throw new Exception("Invalid command line arguments.");

                if (b.Run() == false)
                    throw new Exception($"See the log file: {myApp._LogFile}");
            }
            catch (Exception ex)
            {
                myApp._Log.LogException("RunPERS", ex.ToString(), "EXCEPTION");
                G.DisplayError(ex.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(myApp._LogFile))
                    myApp._Log.WriteExceptionLogToFile(myApp._LogFile, "RunPERS", myApp._TimeTag);
                if (myApp._CognosRaaS != null)
                    myApp._CognosRaaS.Dispose();
                if (myApp._ExcelApp != null)
                    myApp._ExcelApp.Close();
            }

            return;
        }


        private static void HandleParseErrors(IEnumerable<Error> errs)
        {
            throw (new Exception(string.Format("Error parsing command line options.")));
        }

    }  // Class MyProgram


}
