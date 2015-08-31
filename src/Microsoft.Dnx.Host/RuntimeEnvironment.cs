// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace Microsoft.Dnx.Runtime
{
    public class RuntimeEnvironment : IRuntimeEnvironment
    {
        private string _osVersion;

        private string _osName;

        private string _runtimeVersion;

        public RuntimeEnvironment()
        {
#if DNXCORE50
            RuntimeType = RuntimeTypes.CoreCLR;
            RuntimeArchitecture = IntPtr.Size == 8 ? RuntimeArchitectures.X64 : RuntimeArchitectures.X86;
#else
            RuntimeType = Type.GetType("Mono.Runtime") == null ? RuntimeTypes.CLR : RuntimeTypes.Mono;
            RuntimeArchitecture = Environment.Is64BitProcess ? RuntimeArchitectures.X64 : RuntimeArchitectures.X86;
#endif

            // This is a temporary workaround until we pass a struct with OS information from native code
            if (Environment.GetEnvironmentVariable(EnvironmentNames.DnxIsWindows) == "1")
            {
                _osName = RuntimeOperatingSystems.Windows;
            }
        }

        public string OperatingSystem
        {
            get
            {
                if (_osName == null)
                {
                    string uname = NativeMethods.Uname();
                    _osName = string.IsNullOrEmpty(uname) ? RuntimeOperatingSystems.Windows : uname;
                }

                return _osName;
            }
        }

        public string OperatingSystemVersion
        {
            get
            {
                if (OperatingSystem != RuntimeOperatingSystems.Windows)
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
