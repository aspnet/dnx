
namespace Microsoft.Dnx.Runtime
{
    public class CompilerServices
    {
        public CompilerServices(string name, TypeInformation compiler)
        {
            Name = name;
            ProjectCompiler = compiler;
        }

        public string Name { get; private set; }

        public TypeInformation ProjectCompiler { get; private set; }
    }
}