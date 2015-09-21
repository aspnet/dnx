// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Dnx.Host;

namespace Microsoft.Dnx.Runtime
{
    public class RuntimeEnvironment : IRuntimeEnvironment
    {
        private string _osVersion;

        private string _osName;

        private string _runtimeVersion;

        public RuntimeEnvironment(BootstrapperContext bootstrapperContext)
        {
#if DNXCORE50
            RuntimeType = RuntimeTypes.CoreCLR;
#else
            RuntimeType = Type.GetType("Mono.Runtime") == null ? RuntimeTypes.CLR : RuntimeTypes.Mono;
#endif
            RuntimeArchitecture = bootstrapperContext.Architecture;
            _osName = bootstrapperContext.OperatingSystem;
            _osVersion = bootstrapperContext.OsVersion;
        }

        public string OperatingSystem
        {
            get
            {
                return _osName;
            }
        }

        public string OperatingSystemVersion
        {
            get
            {
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
