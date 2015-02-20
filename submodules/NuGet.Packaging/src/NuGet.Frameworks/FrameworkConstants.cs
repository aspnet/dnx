using System;

namespace NuGet.Frameworks
{
    public static class FrameworkConstants
    {
        public const string LessThanOrEqualTo = "\u2264";
        public const string GreaterThanOrEqualTo = "\u2265";
        public static readonly Version EmptyVersion = new Version(0, 0, 0, 0);
        public static readonly Version MaxVersion = new Version(Int32.MaxValue, 0, 0, 0);

        public static class SpecialIdentifiers
        {
            public const string Any = "Any";
            public const string Agnostic = "Agnostic";
            public const string Unsupported = "Unsupported";
        }

        public static class PlatformIdentifiers
        {
            public const string WindowsPhone = "WindowsPhone";
            public const string Windows = "Windows";
        }

        public static class FrameworkIdentifiers
        {
            public const string Net = ".NETFramework";
            public const string NetCore = ".NETCore";
            public const string NetMicro = ".NETMicroFramework";
            public const string Portable = ".NETPortable";
            public const string WindowsPhone = "WindowsPhone";
            public const string Windows = "Windows";
            public const string WindowsPhoneApp = "WindowsPhoneApp";
            public const string AspNet = "ASP.NET";
            public const string AspNetCore = "ASP.NETCore";
            public const string Silverlight = "Silverlight";
            public const string Native = "native";
            public const string MonoAndroid = "MonoAndroid";
            public const string MonoTouch = "MonoTouch";
            public const string MonoMac = "MonoMac";
            public const string XamarinIOs = "Xamarin.iOS";
            public const string XamarinMac = "Xamarin.Mac";
            public const string XamarinPlayStation3 = "Xamarin.PlayStation3";
            public const string XamarinPlayStation4 = "Xamarin.PlayStation4";
            public const string XamarinPlayStationVita = "Xamarin.PlayStationVita";
            public const string XamarinXbox360 = "Xamarin.Xbox360";
            public const string XamarinXboxOne = "Xamarin.XboxOne";
        }

        // public const RegexOptions RegexFlags = RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
        // public static readonly Regex FrameworkRegex = new Regex(@"^(?<Framework>[A-Za-z\.]+)(?<Version>([0-9]+)(\.([0-9]+))*)?(?<Profile>-([A-Za-z]+[0-9]*)+(\+[A-Za-z]+[0-9]*([0-9]+\.([0-9]+))*)*)?$", RegexFlags);
        // public static readonly Regex ProfileNumberRegex = new Regex(@"^Profile(?<ProfileNumber>[0-9]+)$", RegexFlags);

        /// <summary>
        /// Interned frameworks that are commonly used in NuGet
        /// </summary>
        public static class CommonFrameworks
        {
            public static readonly NuGetFramework Net35 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, new Version(3, 5, 0, 0));
            public static readonly NuGetFramework Net4 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, new Version(4, 0, 0, 0));
            public static readonly NuGetFramework Net403 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, new Version(4, 0, 3, 0));
            public static readonly NuGetFramework Net45 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, new Version(4, 5, 0, 0));
            public static readonly NuGetFramework Net451 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, new Version(4, 5, 1, 0));
            public static readonly NuGetFramework Net452 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, new Version(4, 5, 2, 0));
            public static readonly NuGetFramework Net46 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, new Version(4, 6, 0, 0));

            public static readonly NuGetFramework Win8 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(8, 0, 0, 0));
            public static readonly NuGetFramework Win81 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(8, 1, 0, 0));
            public static readonly NuGetFramework Win10 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(10, 0, 0, 0));

            public static readonly NuGetFramework SL4 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(4, 0, 0, 0));
            public static readonly NuGetFramework SL5 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(5, 0, 0, 0));

            public static readonly NuGetFramework WP7 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(7, 0, 0, 0));
            public static readonly NuGetFramework WP75 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(7, 5, 0, 0));
            public static readonly NuGetFramework WP8 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(8, 0, 0, 0));
            public static readonly NuGetFramework WP81 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(8, 1, 0, 0));

            public static readonly NuGetFramework WPA81 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp, new Version(8, 1, 0, 0));

            public static readonly NuGetFramework AspNet = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.AspNet, EmptyVersion);
            public static readonly NuGetFramework AspNetCore = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.AspNetCore, EmptyVersion);
            public static readonly NuGetFramework AspNet50 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.AspNet, new Version(5, 0, 0, 0));
            public static readonly NuGetFramework AspNetCore50 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.AspNetCore, new Version(5, 0, 0, 0));
        }
    }
}
