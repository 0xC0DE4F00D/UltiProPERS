using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PERSReport.Utilities
{
    class ExceptionLog
    {
        public struct ExceptionLogItem
        {
            public string Description;
            public string Request;
            public string Response;
        };

        private List<ExceptionLogItem> _ExceptionLogs;

        public ExceptionLog()
        {
            _ExceptionLogs = new List<ExceptionLogItem>(); ;
        }

        ~ExceptionLog()
        {
            if (_ExceptionLogs != null)
                _ExceptionLogs.Clear();
        }

        public bool LogException(string request, string response, string operation)
        {
            ExceptionLogItem info = new ExceptionLogItem();
            info.Description = operation;
            info.Request = request;
            info.Response = response;
            _ExceptionLogs.Add(info);
            return false;
        }

        public bool WriteExceptionLogToFile(string filename, string source, string queryTimeTag)
        {
            if (_ExceptionLogs.Count == 0)
                return true;

            using (StreamWriter writer = new StreamWriter(filename))
            {
                writer.WriteLine("<records source='{0}' time='{1}'>", source, queryTimeTag);
                foreach (ExceptionLogItem me in _ExceptionLogs)
                {
                    writer.WriteLine("<log Description='{0}'>", me.Description);
                    writer.WriteLine("\t<Request '{0}'/>", me.Request);
                    if (!string.IsNullOrEmpty(me.Response))
                        writer.WriteLine("\t<Response '{0}'/>", G.Truncate(me.Response, 512));
                    writer.WriteLine("</log>");
                }
                writer.WriteLine("</records>");
            }
            return true;
        }
    }
}
