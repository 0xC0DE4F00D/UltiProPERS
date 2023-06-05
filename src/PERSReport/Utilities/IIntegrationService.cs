
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PERSReport.Utilities
{
    interface IIntegrationService
    {
        bool ParseArguments(string[] args);
        bool Run();
    }
}
