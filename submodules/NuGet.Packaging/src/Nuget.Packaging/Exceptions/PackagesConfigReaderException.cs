using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public sealed class PackagesConfigReaderException : PackagingException
    {
        public PackagesConfigReaderException(string message)
            : base(message)
        {

        }
    }
}
