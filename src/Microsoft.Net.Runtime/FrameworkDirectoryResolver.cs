using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Net.Runtime
{
    public static class FrameworkDirectoryResolver
    {
        public static IEnumerable<string> GetFrameworkDirectories()
        {
            string klrPath = Environment.GetEnvironmentVariable("KLR_PATH");

            if (!String.IsNullOrEmpty(klrPath))
            {
                klrPath = Path.GetDirectoryName(klrPath);

                return new[] {
                    Path.GetFullPath(Path.Combine(klrPath, @"..\..\..\Framework")),
#if DEBUG
                    Path.GetFullPath(Path.Combine(klrPath, @"..\..\artifacts\sdk\Framework"))
#endif
                };
            }

            return new string[0];
        }

        
    }
}
