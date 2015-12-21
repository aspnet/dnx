// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Dnx.Host;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime
{
    public class RuntimeEnvironment : IRuntimeEnvironment
    {
        private string _runtimeVersion;

        public RuntimeEnvironment(BootstrapperContext bootstrapperContext)
        {
            // Use the DefaultRuntimeEnvironment to detect the OS details rather than bootstrapper context (temporary, this is probably going away anyway)
            var defaultEnv = new DefaultRuntimeEnvironment();
            OperatingSystem = defaultEnv.OperatingSystem;
            OperatingSystemVersion = defaultEnv.OperatingSystemVersion;
            OperatingSystemPlatform = defaultEnv.OperatingSystemPlatform;

            RuntimeType = bootstrapperContext.RuntimeType;
            RuntimeArchitecture = bootstrapperContext.Architecture;
            RuntimePath = bootstrapperContext.RuntimeDirectory;
        }

        public string OperatingSystem { get; }

        public string OperatingSystemVersion { get; }

        public Platform OperatingSystemPlatform { get; }

        public string RuntimeType { get; }

        public string RuntimeArchitecture { get; }

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

        public string RuntimePath { get; }
    }
}
