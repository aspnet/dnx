using System;
using System.Collections.Generic;

namespace NuGet.ContentModel
{
    public class ContentItem
    {
        public string Path { get; set; }
        public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        public override string ToString()
        {
            return Path;
        }
    }
}