// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling
{
    public static class VersionUtils
    {
        private static readonly Lazy<string> _activeRuntime = new Lazy<string>(GetActiveRuntimeName);

        public static string ActiveRuntimeFullName
        {
            get
            {
                return _activeRuntime.Value;
            }
        }

        private static string GetActiveRuntimeName()
        {
            string pathVariable = Environment.GetEnvironmentVariable("PATH");

            if (!string.IsNullOrEmpty(pathVariable))
            {
                string dnuExecutable = RuntimeEnvironmentHelper.IsWindows ? "dnu.cmd" : "dnu";

                foreach (string folder in pathVariable.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string dnuPath = Path.Combine(folder, dnuExecutable);
                    if (File.Exists(dnuPath) &&
                        string.Equals("bin", Directory.GetParent(dnuPath).Name))
                    {
                        // We found it
                        return Directory.GetParent(folder).Name;
                    }
                }
            }

            return null;
        }
    }
}