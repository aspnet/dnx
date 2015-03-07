// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;

namespace dnx.host
{
    internal class ApplicationEnvironment : IApplicationEnvironment
    {
        private readonly Assembly _assembly;
        private AssemblyName _assemblyName;

        public ApplicationEnvironment(string appBase, FrameworkName targetFramework, string configuration, Assembly assembly)
        {
            _assembly = assembly;

            ApplicationBasePath = appBase;
            RuntimeFramework = targetFramework;
            Configuration = configuration;
        }

        public string ApplicationName => AssemblyName.Name;

        public string Configuration
        {
            get;
            private set;
        }

        public string Version => AssemblyName.Version.ToString();

        public string ApplicationBasePath
        {
            get;
            private set;
        }

        public FrameworkName RuntimeFramework
        {
            get;
            private set;
        }

        private AssemblyName AssemblyName
        {
            get
            {
                if (_assemblyName == null)
                {
                    _assemblyName = _assembly.GetName();
                }

                return _assemblyName;
            }
        }
    }
}
