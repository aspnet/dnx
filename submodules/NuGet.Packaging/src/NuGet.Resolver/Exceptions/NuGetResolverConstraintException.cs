using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    /// <summary>
    /// Constraint exception. Thrown when a solution cannot be found.
    /// </summary>
    public class NuGetResolverConstraintException : NuGetResolverException
    {
        public NuGetResolverConstraintException(string message)
            : base(message)
        {

        }
    }
}
