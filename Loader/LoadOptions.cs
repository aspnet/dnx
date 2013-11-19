using System.Collections.Generic;
namespace Loader
{
    public class LoadOptions
    {
        public string OutputPath { get; set; }

        public string AssemblyName { get; set; }

        public IList<string> CleanArtifacts { get; set; }
    }
}
