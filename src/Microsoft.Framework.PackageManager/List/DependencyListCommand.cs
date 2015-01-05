// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.PackageManager.List
{
    internal class DependencyListCommand
    {
        private readonly DependencyListOptions _options;

        public DependencyListCommand(DependencyListOptions options)
        {
            _options = options;
        }

        public int Execute()
        {
            var result = 0;
            _options.Reports.Information.WriteLine("List dependencies for {0} ({1})", _options.Project.Name, _options.Project.ProjectFilePath);

            var frameworks = new HashSet<FrameworkName>(_options.Project.GetTargetFrameworks().Select(f => f.FrameworkName));
            if (_options.Framework != null)
            {
                if (frameworks.Contains(_options.Framework))
                {
                    frameworks.Clear();
                    frameworks.Add(_options.Framework);
                }
                else
                {
                    _options.Reports.Error.WriteLine("Project doesn't support framework: {0}", _options.Framework.FullName);
                    return 0;
                }
            }

            foreach (var framework in frameworks)
            {
                _options.Reports.Information.WriteLine("[Target framework {0}]", framework.Identifier.ToString());

                var operation = new DependencyListOperation(_options, framework);

                if (!operation.Execute())
                {
                    _options.Reports.Error.WriteLine("There was an error listing the dependencies");
                    return 3;
                }
            }

            return result;
        }
    }
}