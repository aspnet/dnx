using System;
using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager
{
    public interface IReport
    {
        void WriteLine(string message);
    }
}