using System.Collections.Generic;

namespace NuGet.ContentModel
{
    public class ContentItemGroup
    {
        public ContentItemGroup()
        {
            Properties = new Dictionary<string, object>();
            Items = new List<ContentItem>();
        }

        public IDictionary<string, object> Properties { get;  }

        public IList<ContentItem> Items { get;  }
    }
}
