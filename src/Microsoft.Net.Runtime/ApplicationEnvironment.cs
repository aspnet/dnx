using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Net.Runtime.Services;

namespace Microsoft.Net.Runtime
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


        public FrameworkName TargetFramework
        {
            get { return _targetFramework; }
        }
    }
}
