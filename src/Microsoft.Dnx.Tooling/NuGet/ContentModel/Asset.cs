using System;

namespace NuGet.ContentModel
{
    public class Asset
    {
        public string Path { get; set; }
        public string Link { get; set; }

        public override string ToString()
        {
            return Path;
        }
    }
}
