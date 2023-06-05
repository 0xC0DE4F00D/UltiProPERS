using System;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using PERSReport.com.ultipro.service5.BIDataService;
using PERSReport.com.ultipro.service5.BIStreamingService;
using PERSReport.Utilities;

namespace PERSReport.Services
{
    class CognosReportAsAService : IDisposable
    {
        BIDataServiceClient _BiDataSvcClient;
        DataContext _DataSvcContext;
        ExceptionLog _myAppExLog;

        public CognosReportAsAService()
        {
            // Invoke "Initialize" to establish these dependencies.
            _BiDataSvcClient = null;
            _DataSvcContext = null;
            _myAppExLog = null;
        }

        public CognosReportAsAService(ref ExceptionLog exceptionLog)
        {
            _myAppExLog = exceptionLog;
            _BiDataSvcClient = null;
            _DataSvcContext = null;
        }

        ~CognosReportAsAService()
        {
            _myAppExLog = null;
        }

        public void Dispose()
        {
            try
            {
                Debug.WriteLine("CognosReportAsAService : Dispose");

                if (_BiDataSvcClient != null)
                {
                    _BiDataSvcClient.LogOff(_DataSvcContext);
                    CloseClientProxy(_BiDataSvcClient);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                _myAppExLog.LogException("CognosReportAsAService::Dispose", ex.Message, "EXCEPTION");
            }
        }

        private static void CloseClientProxy(ICommunicationObject client)
        {
            if (client == null) return;
            if (client.State != CommunicationState.Faulted)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    client.Abort();
                }
            }
            else
            {
                client.Abort();
            }
            return;
        }

        public bool Initialize(string clientAccessKey, string userName, string passWord, string userAccessKey)
        {
            Debug.Assert(_BiDataSvcClient == null);

            try
            {
                _BiDataSvcClient = new BIDataServiceClient("WSHttpBinding_IBIDataService");
                _DataSvcContext = _BiDataSvcClient.LogOn(new LogOnRequest
                {
                    ClientAccessKey = clientAccessKey,
                    UserName = userName,
                    Password = passWord,
                    UserAccessKey = userAccessKey
                });

                if (_DataSvcContext.Status != ContextStatus.Ok)
                {
                    Debug.WriteLine(_DataSvcContext.StatusMessage);
                    if (_myAppExLog != null) _myAppExLog.LogException("CognosReportAsAService::Initialize", _DataSvcContext.StatusMessage, "ERROR");
                    return false;
                }
            }
            catch (CommunicationException ex)
            {
                Debug.WriteLine(ex.Message);
                if (_myAppExLog != null) _myAppExLog.LogException("CognosReportAsAService::Initialize", ex.Message, "EXCEPTION");
                return false;
            }
            return true;
        }


#if DEBUG
        public bool TestRaaS()
        {
            //if (GetReportList() == false)
            //    throw (new Exception("Failed to GetReportList from Cognos Web Services. Further processing halted."));

            if (GetReportParameters(Properties.Settings.Default.RunPERSbyPeriodControlDateV3ID) == false)
                throw (new Exception("Failed to GetReportParameters from Cognos Web Services. Further processing halted."));

            //if (GetPersByDailyHours(RunPERS.Properties.Settings.Default.RunPERSbyDailyHoursID, @"8/21/2020", @"9/3/2020", @"..\..\TEST\TestReport.csv") == false)
            //    throw (new Exception($"Failed to GetReport from Cognos Web Services. ReportID={RunPERS.Properties.Settings.Default.RunPERSbyPeriodControlDateV2ID}"));

            return true;
        }


        public bool GetReportList()
        {
            Debug.Assert(_BiDataSvcClient != null);
            Debug.Assert(_DataSvcContext != null);

            try
            {
                using (new OperationContextScope(_BiDataSvcClient.InnerChannel))
                {
                    var httpHeader = new HttpRequestMessageProperty();

                    ReportListResponse response = _BiDataSvcClient.GetReportList(_DataSvcContext);
                    if (response.Status == ReportRequestStatus.Success)
                    {
                        foreach (Report r in response.Reports)
                        {
                            Debug.WriteLine("{0} {1}", r.ReportName, r.ReportPath);   // Set a breakpoint herein to view the list of reports in the Output window.
                        }
                    }
                    else
                    {
                        Debug.WriteLine(response.StatusMessage);
                        _myAppExLog.LogException("CognosReportAsAService::GetReportList", response.StatusMessage, "ERROR");
                        return false;
                    }
                }
            }
            catch (CommunicationException ex)
            {
                Debug.WriteLine(ex.Message);
                _myAppExLog.LogException("CognosReportAsAService::GetReportList", ex.Message, "EXCEPTION");
                return false;
            }
            return true;
        }


        public bool GetReportParameters(string reportID)
        {
            Debug.Assert(_BiDataSvcClient != null);
            Debug.Assert(_DataSvcContext != null);

            try
            {
                using (new OperationContextScope(_BiDataSvcClient.InnerChannel))
                {
                    var httpHeader = new HttpRequestMessageProperty();

                    string reportPath = reportID;

                    ReportParameterResponse response = _BiDataSvcClient.GetReportParameters(reportPath, _DataSvcContext);

                    if (response.Status == ReportRequestStatus.Success)
                    {
                        foreach (ReportParameter p in response.ReportParameters)  // look at the output window for a parameter list export
                        {
                            Debug.WriteLine("Name={0}, Value={1}, Required={2}, DataType={3}, MultiValued={4}", p.Name, p.Value, p.Required, p.DataType, p.MultiValued);
                        }
                    }
                    else
                    {
                        Debug.WriteLine(response.StatusMessage);
                        _myAppExLog.LogException("CognosReportAsAService::GetReportParameters", response.StatusMessage, "ERROR");
                        return false;
                    }
                }
            }
            catch (CommunicationException ex)
            {
                Debug.WriteLine(ex.Message);
                _myAppExLog.LogException("CognosReportAsAService::GetReportParameters", ex.Message, "EXCEPTION");
                return false;
            }
            return true;
        }
#endif 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reportID">Example: "storeID(\"i914CC8CD9C9447E3B123A9F9BB247DDE\")</param>
        /// <param name="parameters"></param>
        /// <param name="xmlResponse"></param>
        /// <returns></returns>
        public bool GetReportByID(string reportID, ReportParameter[] parameters, string filePath)
        {
            Debug.Assert(_BiDataSvcClient != null);
            Debug.Assert(_DataSvcContext != null);

            try
            {
                using (new OperationContextScope(_BiDataSvcClient.InnerChannel))
                {
                    var httpHeader = new HttpRequestMessageProperty();

                    //Comment -out the next 2 lines if you want the output to be in XML - normal is XML DataSet export.
                    httpHeader.Headers["US-DELIMITER"] = ",";    // use "," or use "SP" for space or "HT" for '|' delimeter.
                    OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = httpHeader;

                    ReportResponse response = _BiDataSvcClient.ExecuteReport(new ReportRequest
                    {
                        ReportPath = reportID,
                        ReportParameters = parameters,
                    }, _DataSvcContext);

                    if (response.Status == ReportRequestStatus.Success)
                    {
                        if (WriteResponseToFile(response, filePath) == false)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        Debug.WriteLine(response.StatusMessage);
                        _myAppExLog.LogException("CognosReportAsAService::GetReportByID", response.StatusMessage, "ERROR");
                        return false;
                    }
                }
            }
            catch (CommunicationException ex)
            {
                Debug.WriteLine(ex.Message);
               _myAppExLog.LogException("CognosReportAsAService::GetReportByID", ex.Message, "EXCEPTION");
                return false;
            }
            return true;
        }


        private bool WriteResponseToFile(ReportResponse response, string filePath)   //out string xmlResponse)
        {
            string msg;
            Stream fromWeb = null;
            BIStreamServiceClient streamClient = null;

            try
            {
                streamClient = new BIStreamServiceClient("WSHttpBinding_IBIStreamService",
                                                         new EndpointAddress(response.ReportRetrievalUri));
                ReportResponseStatus status;
                do
                {
                    status = streamClient.RetrieveReport(response.ReportKey, out msg, out fromWeb);
                } while (status == ReportResponseStatus.Working);

                if (status == ReportResponseStatus.Failed)
                {
                    Debug.WriteLine(msg);
                    _myAppExLog.LogException("CognosReportAsAService::WriteResponseToFile", msg, "ERROR");
                    return false;
                }

                using (StreamReader reader = new StreamReader(fromWeb))
                {
                    using (Stream output = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        using (StreamWriter writer = new StreamWriter(output))
                        {
                            int bytesRead;
                            char[] buffer = new char[4096];
                            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                writer.Write(buffer, 0, bytesRead);
                            }
                        }
                    }
                }
            }
            catch (CommunicationException ex)
            {
                Debug.WriteLine(ex.Message);
                _myAppExLog.LogException("CognosReportAsAService::WriteResponseToFile", ex.Message, "EXCEPTION");
                return false;
            }
            finally
            {
                if (fromWeb != null)
                {
                    fromWeb.Close();
                }
                CloseClientProxy(streamClient);
            }
            return true;
        }


        public bool GetPersByPeriodControlDate(string reportID, string periodControlDate, string filePath)
        {
            var reportParameters = new ReportParameter[]{ new ReportParameter
                                                            {
                                                                Name = "Period_Control_Date",
                                                                Value = periodControlDate,
                                                                Required = true,
                                                                DataType = "xsdDateTime",
                                                                MultiValued = false
                                                            }
                                                        };

            return GetReportByID(reportID, reportParameters, filePath);
        }


        public bool GetPersByDailyHours(string reportID, string periodControlDate, string filePath)
        {
            var reportParameters = new ReportParameter[]{  new ReportParameter
                                                            {
                                                                Name = "Period_Control_Date",
                                                                Value = periodControlDate,
                                                                Required = true,
                                                                DataType = "xsdDateTime",
                                                                MultiValued = false
                                                            }
                                                        };
            return(GetReportByID(reportID, reportParameters, filePath));
        }


        public bool GetPersContributionRates(string reportID, string periodControlDate, string filePath)
        {
            var reportParameters = new ReportParameter[]{  new ReportParameter
                                                            {
                                                                Name = "Period_Control_Date",
                                                                Value = periodControlDate,
                                                                Required = true,
                                                                DataType = "xsdDateTime",
                                                                MultiValued = false
                                                            }
                                                        };
            return (GetReportByID(reportID, reportParameters, filePath));
        }
    }
}
