// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Framework.PackageManager
{
    internal static class PathUtilities
    {
        private static Lazy<string> _dotNetHome = new Lazy<string>(FindDotNetHome);

        public static string DotNetHomeFolder
        {
            get
            {
                return _dotNetHome.Value;
            }
        }

        public static string DotNetBinFolder
        {
            get
            {
                if (DotNetHomeFolder == null)
                {
                    return null;
                }

                return Path.Combine(DotNetHomeFolder, "bin");
            }
        }

        private static string FindDotNetHome()
        {
            // TODO: remove KRE_HOME
            var dotnetHome = Environment.GetEnvironmentVariable("DOTNET_HOME") ?? Environment.GetEnvironmentVariable("KRE_HOME");
            if (string.IsNullOrEmpty(dotnetHome))
            {
                var dotnetGlobalPath = Environment.GetEnvironmentVariable("DOTNET_GLOBAL_PATH");

                var userProfileFolder = Environment.GetEnvironmentVariable("USERPROFILE");
                if (string.IsNullOrEmpty(userProfileFolder))
                {
                    userProfileFolder = Environment.GetEnvironmentVariable("HOME");
                }

                string userDotNetFolder = null;
                if (!string.IsNullOrEmpty(userProfileFolder))
                {
                    userDotNetFolder = Path.Combine(userProfileFolder, ".dotnet");
                }

                dotnetHome = userDotNetFolder + dotnetGlobalPath;
            }

            foreach (var probePath in dotnetHome.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string fullPath = Environment.ExpandEnvironmentVariables(probePath);

                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }
    }
}