using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    internal class GetCompiledAssemblyMessage : DesignTimeMessage
    {
        public GetCompiledAssemblyMessage()
        {
            MessageType = "GetCompiledAssembly";
        }

        public string Name { get; set; }
        public string Configuration { get; set; }
        public FrameworkName TargetFramework { get; set; }
        public string Aspect { get; set; }

        protected override string GetPayload()
        {
            return $@"
    ""Name"": ""{Name}"",
    ""Configuration"": ""{Configuration}"",
    ""TargetFramework"": ""{TargetFramework.ToString()}"",
    ""Aspect"": ""{Aspect}""";
        }
    }
}
