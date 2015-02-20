using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    public class NuGetResolverException : Exception
    {
        public NuGetResolverException(string message)
            : base(message)
        {

        }
    }
}
