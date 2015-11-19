using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.ContentModel
{
    public class ContentPropertyDefinition
    {
        public ContentPropertyDefinition()
        {
            Table = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            FileExtensions = new List<string>();
        }

        public IDictionary<string, object> Table { get; set; }

        public List<string> FileExtensions { get; set; }

        public bool FileExtensionAllowSubFolders { get; set; }

        public Func<string, object> Parser { get; set; }

        public virtual bool TryLookup(string name, out object value)
        {
            if (name == null)
            {
                value = null;
                return false;
            }

            if (Table != null && Table.TryGetValue(name, out value))
            {
                return true;
            }

            if (FileExtensions != null && FileExtensions.Any())
            {
                if (FileExtensionAllowSubFolders == true || name.IndexOfAny(new[] { '/', '\\' }) == -1)
                {
                    foreach (var fileExtension in FileExtensions)
                    {
                        if (name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        {

                            value = name;
                            return true;
                        }
                    }
                }
            }

            if (Parser != null)
            {
                value = Parser.Invoke(name);
                if (value != null)
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        public Func<object, object, bool> OnIsCriteriaSatisfied { get; set; } = Object.Equals;
        public Func<object, object, object, int> OnCompare { get; set; }

        public virtual bool IsCriteriaSatisfied(object critieriaValue, object candidateValue)
        {
            return OnIsCriteriaSatisfied.Invoke(critieriaValue, candidateValue);
        }

        public virtual int Compare(object criteriaValue, object candidateValue1, object candidateValue2)
        {
            if (OnCompare != null)
            {
                return OnCompare(criteriaValue, candidateValue1, candidateValue2);
            }

            var betterCoverageFromValue1 = IsCriteriaSatisfied(candidateValue1, candidateValue2);
            var betterCoverageFromValue2 = IsCriteriaSatisfied(candidateValue2, candidateValue1);
            if (betterCoverageFromValue1 && !betterCoverageFromValue2)
            {
                return -1;
            }
            if (betterCoverageFromValue2 && !betterCoverageFromValue1)
            {
                return 1;
            }
            return 0;
        }
    }
}
