using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class FrameworkException : Exception
    {

        public FrameworkException(string message)
            : base(message)
        {

        }

    }
}
