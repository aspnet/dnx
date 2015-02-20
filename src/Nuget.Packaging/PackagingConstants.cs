using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public static class PackagingConstants
    {
        public const string ContentFolder = "content";
        public const string AnyFramework = "any";
        public const string AgnosticFramework = "agnostic";

        public const string TargetFrameworkPropertyKey = "targetframework";

        // should this logic be taken out of packaging?
        public const RegexOptions RegexFlags = RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
        public static readonly Regex FrameworkRegex = new Regex(@"^(?<Framework>[A-Za-z]+)(?<Version>([0-9]+)(\.([0-9]+))*)?(?<Profile>-([A-Za-z]+[0-9]*)+(\+[A-Za-z]+[0-9]*)*)?$", RegexFlags);
    }
}
