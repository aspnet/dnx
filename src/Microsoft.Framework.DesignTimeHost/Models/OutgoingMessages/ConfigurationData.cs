using System;
using Microsoft.Framework.Runtime.Roslyn;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ConfigurationData
    {
        public string Name { get; set; }

        public CompilationSettings CompilationSettings { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ConfigurationData;

            return other != null &&
                   string.Equals(Name, other.Name) &&
                   object.Equals(CompilationSettings, other.CompilationSettings);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}