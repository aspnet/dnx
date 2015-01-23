// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Framework.PackageManager
{
    internal static class PathUtilities
    {
        private static Lazy<string> _runtimeHome = new Lazy<string>(FindRuntimeHome);

        public static string RuntimeHomeFolder
        {
            get
            {
                return _runtimeHome.Value;
            }
        }

        public static string RuntimeBinFolder
        {
            get
            {
                if (RuntimeHomeFolder == null)
                {
                    return null;
                }

                return Path.Combine(RuntimeHomeFolder, "bin");
            }
        }

        private static string FindRuntimeHome()
        {
            var runtimeHome = Environment.GetEnvironmentVariable("KRE_HOME");
            if (string.IsNullOrEmpty(runtimeHome))
            {
                var runtimeGlobalPath = Environment.GetEnvironmentVariable("KRE_GLOBAL_PATH");

                var userProfileFolder = Environment.GetEnvironmentVariable("USERPROFILE");
                if (string.IsNullOrEmpty(userProfileFolder))
                {
                    userProfileFolder = Environment.GetEnvironmentVariable("HOME");
                }

                string userRuntimeFolder = null;
                if (!string.IsNullOrEmpty(userProfileFolder))
                {
                    userRuntimeFolder = Path.Combine(userProfileFolder, Runtime.Constants.DefaultLocalRuntimeHomeDir);
                }

                runtimeHome = userRuntimeFolder + runtimeGlobalPath;
            }

            foreach (var probePath in runtimeHome.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
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