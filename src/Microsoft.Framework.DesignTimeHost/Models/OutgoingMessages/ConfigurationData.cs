using System;
using Microsoft.Framework.Runtime.Roslyn;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ConfigurationData
    {
        public string Name { get; set; }

        public CompilationSettings CompilationSettings { get; set; }
    }
}