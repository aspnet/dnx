using System;
using System.Collections.Generic;

namespace NuGet.ContentModel
{
    public class ContentPatternDefinition
    {
        public ContentPatternDefinition()
        {
            GroupPatterns = new List<PatternDefinition>();
            PathPatterns = new List<PatternDefinition>();
            PropertyDefinitions = new Dictionary<string, ContentPropertyDefinition>();
        }
        public IList<PatternDefinition> GroupPatterns { get; set; }

        public IList<PatternDefinition> PathPatterns { get; set; }

        public IDictionary<string, ContentPropertyDefinition> PropertyDefinitions { get; set; }
    }

    public class PatternDefinition
    {
        public string Pattern { get; set; }

        public IDictionary<string, object> Defaults { get; set; } = new Dictionary<string, object>();

        public static implicit operator PatternDefinition(string pattern)
        {
            return new PatternDefinition { Pattern = pattern };
        }
    }
}
