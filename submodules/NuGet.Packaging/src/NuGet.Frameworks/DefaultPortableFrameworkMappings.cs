using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    /// <summary>
    /// Contains the standard portable framework mappings
    /// </summary>
    public class DefaultPortableFrameworkMappings : IPortableFrameworkMappings
    {
        public DefaultPortableFrameworkMappings()
        {

        }

        private KeyValuePair<int, NuGetFramework[]>[] _profileFrameworks;
        public IEnumerable<KeyValuePair<int, NuGetFramework[]>> ProfileFrameworks
        {
            get
            {
                if (_profileFrameworks == null)
                {
                    NuGetFramework net4 = FrameworkConstants.CommonFrameworks.Net4;
                    NuGetFramework net403 = FrameworkConstants.CommonFrameworks.Net403;
                    NuGetFramework net45 = FrameworkConstants.CommonFrameworks.Net45;
                    NuGetFramework net451 = FrameworkConstants.CommonFrameworks.Net451;

                    NuGetFramework win8 = FrameworkConstants.CommonFrameworks.Win8;
                    NuGetFramework win81 = FrameworkConstants.CommonFrameworks.Win81;

                    NuGetFramework sl4 = FrameworkConstants.CommonFrameworks.SL4;
                    NuGetFramework sl5 = FrameworkConstants.CommonFrameworks.SL5;

                    NuGetFramework wp7 = FrameworkConstants.CommonFrameworks.WP7;
                    NuGetFramework wp75 = FrameworkConstants.CommonFrameworks.WP75;
                    NuGetFramework wp8 = FrameworkConstants.CommonFrameworks.WP8;
                    NuGetFramework wp81 = FrameworkConstants.CommonFrameworks.WP81;

                    NuGetFramework wpa81 = FrameworkConstants.CommonFrameworks.WPA81;

                    _profileFrameworks = new KeyValuePair<int,NuGetFramework[]>[]
                    {
                        // v4.6
                        CreateProfileFrameworks(31, win81, wp81),
                        CreateProfileFrameworks(32, win81, wpa81),
                        CreateProfileFrameworks(44, net451, win81),
                        CreateProfileFrameworks(84, wp81, wpa81),
                        CreateProfileFrameworks(151, net451, win81, wpa81),
                        CreateProfileFrameworks(157, win81, wp81, wpa81),

                        // v4.5
                        CreateProfileFrameworks(7, net45, win8),
                        CreateProfileFrameworks(49, net45, wp8),
                        CreateProfileFrameworks(78, net45, win8, wp8),
                        CreateProfileFrameworks(111, net45, win8, wpa81),
                        CreateProfileFrameworks(259, net45, win8, wpa81, wp8),

                        // v4.0
                        CreateProfileFrameworks(2, net4, win8, sl4, wp7),
                        CreateProfileFrameworks(3, net4, sl4),
                        CreateProfileFrameworks(4, net45, sl4, win8, wp7),
                        CreateProfileFrameworks(5, net4, win8),
                        CreateProfileFrameworks(6, net403, win8),
                        CreateProfileFrameworks(14, net4, sl5),
                        CreateProfileFrameworks(18, net403, sl4),
                        CreateProfileFrameworks(19, net403, sl5),
                        CreateProfileFrameworks(23, net45, sl4),
                        CreateProfileFrameworks(24, net45, sl5),
                        CreateProfileFrameworks(36, net4, sl4, win8, wp8),
                        CreateProfileFrameworks(37, net4, sl5, win8),
                        CreateProfileFrameworks(41, net403, sl4, win8),
                        CreateProfileFrameworks(42, net403, sl5, win8),
                        CreateProfileFrameworks(46, net45, sl4, win8),
                        CreateProfileFrameworks(47, net45, sl5, win8),
                        CreateProfileFrameworks(88, net4, sl4, win8, wp75),
                        CreateProfileFrameworks(92, net4, win8, wpa81),
                        CreateProfileFrameworks(95, net403, sl4, win8, wp7),
                        CreateProfileFrameworks(96, net403, sl4, win8, wp75),
                        CreateProfileFrameworks(102, net403, win8, wpa81),
                        CreateProfileFrameworks(104, net45, sl4, win8, wp75),
                        CreateProfileFrameworks(136, net4, sl5, win8, wp8),
                        CreateProfileFrameworks(143, net403, sl4, win8, wp8),
                        CreateProfileFrameworks(147, net403, sl5, win8, wp8),
                        CreateProfileFrameworks(154, net45, sl4, win8, wp8),
                        CreateProfileFrameworks(158, net45, sl5, win8, wp8),
                        CreateProfileFrameworks(225, net4, sl5, win8, wpa81),
                        CreateProfileFrameworks(240, net403, sl5, win8, wpa81),
                        CreateProfileFrameworks(255, net45, sl5, win8, wpa81),
                        CreateProfileFrameworks(328, net4, sl5, win8, wpa81, wp8),
                        CreateProfileFrameworks(336, net403, sl5, win8, wpa81, wp8),
                        CreateProfileFrameworks(344, net45, sl5, win8, wpa81, wp8),
                    };
                }

                return _profileFrameworks;
            }
        }

        private KeyValuePair<int, NuGetFramework[]> CreateProfileFrameworks(int profile, params NuGetFramework[] frameworks)
        {
            return new KeyValuePair<int, NuGetFramework[]>(profile, frameworks);
        }

        // profiles that also support monotouch1+monoandroid1
        private static int[] _profilesWithOptionalFrameworks = new int[] { 
            5, 6, 7, 14, 19, 24, 37, 42, 47, 49, 78, 92, 102, 111, 136, 147, 158, 225, 255, 259, 328, 336, 344 
        };

        private List<KeyValuePair<int, NuGetFramework[]>> _profileOptionalFrameworks;
        public IEnumerable<KeyValuePair<int, NuGetFramework[]>> ProfileOptionalFrameworks
        {
            get
            {
                if (_profileOptionalFrameworks == null)
                {
                    List<KeyValuePair<int, NuGetFramework[]>> profileOptionalFrameworks = null;

                    NuGetFramework monoandroid = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.MonoAndroid, new Version(0, 0));
                    NuGetFramework monotouch = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.MonoTouch, new Version(0, 0));
                    NuGetFramework[] monoFrameworks = new NuGetFramework[] { monoandroid, monotouch };

                    profileOptionalFrameworks = new List<KeyValuePair<int, NuGetFramework[]>>();

                    foreach (int profile in _profilesWithOptionalFrameworks)
                    {
                        profileOptionalFrameworks.Add(new KeyValuePair<int, NuGetFramework[]>(profile, monoFrameworks));
                    }

                    _profileOptionalFrameworks = profileOptionalFrameworks;
                }

                return _profileOptionalFrameworks;
            }
        }

        private static IPortableFrameworkMappings _instance;

        /// <summary>
        /// Static instance of the portable framework mappings
        /// </summary>
        public static IPortableFrameworkMappings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DefaultPortableFrameworkMappings();
                }

                return _instance;
            }
        }
    }
}
