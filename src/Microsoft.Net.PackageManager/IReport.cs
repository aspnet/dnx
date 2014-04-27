using System;
using System.Collections.Generic;
using System.Web;

namespace Microsoft.Net.PackageManager
{
    public interface IReport
    {
        void WriteLine(string message);
    }
}