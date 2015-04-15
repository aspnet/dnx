// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class RuntimeEnvironment : IRuntimeEnvironment
    {
        private string _runtimeVersion;

        public RuntimeEnvironment()
        {
#if DNXCORE50
            RuntimeType = "CoreCLR";
            RuntimeArchitecture = IntPtr.Size == 8 ? "x64" : "x86";
#else
            RuntimeType = Type.GetType("Mono.Runtime") == null ? "CLR" : "Mono";
            RuntimeArchitecture = Environment.Is64BitProcess ? "x64" : "x86";
#endif

            string uname = NativeMethods.Uname();
            if (!string.IsNullOrEmpty(uname))
            {
                OperatingSystem = uname;
                OperatingSystemVersion = null;
            }
            else
            {
                OperatingSystem = "Windows";
#if DNXCORE50
                OperatingSystemVersion = NativeMethods.OSVersion.ToString();
#else
                OperatingSystemVersion = Environment.OSVersion.Version.ToString();
#endif
            }
        }

        public string OperatingSystem { get; private set; }

        public string OperatingSystemVersion { get; private set; }

        public string RuntimeType { get; private set; }

        public string RuntimeArchitecture { get; private set; }

        public string RuntimeVersion
        {
            get
            {
                if (_runtimeVersion == null)
                {
                    _runtimeVersion = typeof(RuntimeEnvironment)
                        .GetTypeInfo()
                        .Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                        .InformationalVersion;
                }

                return _runtimeVersion;

            }
        }
    }
}
