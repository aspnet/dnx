using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    /// <summary>
    /// Input validation exception
    /// </summary>
    public class NuGetResolverInputException : NuGetResolverException
    {
        public NuGetResolverInputException(string message)
            : base(message)
        {

        }
    }
}
