// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Resources;
using System.Threading;
using System.Reflection;

namespace NuGet
{
    internal static class LocalizedResourceManager
    {
        private static readonly ResourceManager _resourceManager = new ResourceManager("Microsoft.Dnx.Tooling.NuGet.NuGetResources", typeof(LocalizedResourceManager).GetTypeInfo().Assembly);

        public static string GetString(string resourceName)
        {
            var culture = GetLanguageName();
            return _resourceManager.GetString(resourceName + '_' + culture, CultureInfo.InvariantCulture) ??
                   _resourceManager.GetString(resourceName, CultureInfo.InvariantCulture);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "the convention is to used lower case letter for language name.")]        
        /// <summary>
        /// Returns the 3 letter language name used to locate localized resources.
        /// </summary>
        /// <returns>the 3 letter language name used to locate localized resources.</returns>
        public static string GetLanguageName()
        {
#if DNX451
            var culture = Thread.CurrentThread.CurrentUICulture;
            while (!culture.IsNeutralCulture)
            {
                if (culture.Parent == culture)
                {
                    break;
                }

                culture = culture.Parent;
            }

            return culture.ThreeLetterWindowsLanguageName.ToLowerInvariant();
#else
            return "enu";
#endif
        }
    }
}
