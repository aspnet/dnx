using System;
using System.Collections.Generic;

namespace Microsoft.Net.PackageManager
{
    public interface IReport
    {
        void WriteLine(string message);
    }
}