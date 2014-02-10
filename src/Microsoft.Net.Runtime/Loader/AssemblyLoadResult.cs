using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Net.Runtime.Loader
{
    // REVIEW: Should this be a struct?
    public class AssemblyLoadResult
    {
        public Assembly Assembly { get; private set; }

        public IList<string> Errors { get; private set; }

        public AssemblyLoadResult()
        {
        }

        public AssemblyLoadResult(IList<string> errors)
        {
            Errors = errors;
        }

        public AssemblyLoadResult(Assembly assembly)
        {
            Assembly = assembly;
        }
    }
}
