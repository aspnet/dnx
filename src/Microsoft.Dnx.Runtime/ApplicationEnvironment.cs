// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Dnx.Runtime.Sources.Impl;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Application environment built by the application host.
    /// </summary>
    public class ApplicationEnvironment : IApplicationEnvironment
    {
        private readonly Project _project;
        private readonly FrameworkName _targetFramework;
        private readonly ApplicationGlobalData _globalData;

        public ApplicationEnvironment(Project project, FrameworkName targetFramework, string configuration, IApplicationEnvironment hostEnvironment)
        {
            _project = project;
            _targetFramework = targetFramework;
            Configuration = configuration;

            _globalData = new ApplicationGlobalData(hostEnvironment);
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

        public string Configuration { get; private set; }


        public FrameworkName RuntimeFramework
        {
            get { return _targetFramework; }
        }

        public object GetData(string name)
        {
            return _globalData.GetData(name);
        }

        public void SetData(string name, object value)
        {
            _globalData.SetData(name, value);
        }
    }
}
