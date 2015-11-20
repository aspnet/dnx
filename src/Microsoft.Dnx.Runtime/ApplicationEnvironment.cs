// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Application environment built by the application host.
    /// </summary>
    public class ApplicationEnvironment : IApplicationEnvironment
    {
        private readonly Project _project;
        private readonly FrameworkName _targetFramework;

        public ApplicationEnvironment(Project project, FrameworkName targetFramework, IApplicationEnvironment hostEnvironment)
        {
            _project = project;
            _targetFramework = targetFramework;
        }

        public string ApplicationName
        {
            get
            {
                return _project.EntryPoint ?? _project.Name;
            }
        }

        public string ApplicationBasePath
        {
            get
            {
                return _project.ProjectDirectory;
            }
        }

        public string ApplicationVersion
        {
            get { return _project.Version.ToString(); }
        }

        public FrameworkName RuntimeFramework
        {
            get { return _targetFramework; }
        }
    }
}
