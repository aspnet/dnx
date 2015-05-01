// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class RuntimeEnvironment : IRuntimeEnvironment
    {
        private string _osVersion;

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
            OperatingSystem = string.IsNullOrEmpty(uname) ? "Windows" : uname;
        }

        public string OperatingSystem { get; private set; }

        public string OperatingSystemVersion
        {
            get
            {
                if (OperatingSystem != "Windows")
                {
                    return null;
                }

                if (_osVersion == null)
                {
#if DNXCORE50
                    _osVersion = NativeMethods.OSVersion.ToString();
#else
                    _osVersion = Environment.OSVersion.Version.ToString();
#endif
                }

                return _osVersion;
            }
        }

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
