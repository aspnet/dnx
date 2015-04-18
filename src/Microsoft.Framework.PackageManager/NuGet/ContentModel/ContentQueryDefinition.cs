using System;
using System.Collections.Generic;

namespace NuGet.ContentModel
{
    public class ContentPatternDefinition
    {
        public ContentPatternDefinition()
        {
            GroupPatterns = new List<string>();
            PathPatterns = new List<string>();
            PropertyDefinitions = new Dictionary<string, ContentPropertyDefinition>();
        }
        public IList<string> GroupPatterns { get; set; }

        public IList<string> PathPatterns { get; set; }

        public IDictionary<string, ContentPropertyDefinition> PropertyDefinitions { get; set; }
    }
}
