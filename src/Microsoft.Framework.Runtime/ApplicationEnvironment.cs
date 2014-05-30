// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace Microsoft.Framework.Runtime
{
    public class ApplicationEnvironment : IApplicationEnvironment
    {
        private readonly Project _project;
        private readonly FrameworkName _targetFramework;

        public ApplicationEnvironment(Project project, FrameworkName targetFramework)
        {
            _project = project;
            _targetFramework = targetFramework;
        }

        public string ApplicationName
        {
            get
            {
                return _project.Name;
            }
        }

        public string ApplicationBasePath
        {
            get
            {
                return _project.ProjectDirectory;
            }
        }

        public string Version
        {
            get { return _project.Version.ToString(); }
        }


        public FrameworkName RuntimeFramework
        {
            get { return _targetFramework; }
        }
    }
}
