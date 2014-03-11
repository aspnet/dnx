using System.Runtime.Versioning;
using Microsoft.Net.Runtime.Loader;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynMetadataProvider
    {
        private readonly IRoslynCompiler _compiler;

        public RoslynMetadataProvider(IRoslynCompiler compiler)
        {
            _compiler = compiler;
        }

        public RoslynProjectMetadata GetMetadata(string name, FrameworkName targetFramework)
        {
            var context = _compiler.CompileProject(name, targetFramework);

            if (context == null)
            {
                return null;
            }

            return new RoslynProjectMetadata(context);
        }
    }
}
