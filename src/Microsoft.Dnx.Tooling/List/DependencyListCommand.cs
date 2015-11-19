// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using Microsoft.Dnx.Tooling.Utils;

namespace Microsoft.Dnx.Tooling.List
{
    internal class DependencyListCommand
    {
        private readonly DependencyListOptions _options;
        private readonly FrameworkName _fallbackFramework;

        public DependencyListCommand(DependencyListOptions options, FrameworkName fallbackFramewok)
        {
            _options = options;
            _fallbackFramework = fallbackFramewok;
        }

        public int Execute()
        {
            _options.Reports.Information.WriteLine("Listing dependencies for {0} ({1})", _options.Project.Name, _options.Project.ProjectFilePath);

            string frameworkSelectionError;
            var frameworks = FrameworkSelectionHelper.SelectFrameworks(_options.Project, 
                                                                       _options.TargetFrameworks, 
                                                                       _fallbackFramework, 
                                                                       out frameworkSelectionError);
            if (frameworks == null)
            {
                _options.Reports.Error.WriteLine(frameworkSelectionError);
                return 1;
            } 

            foreach (var framework in frameworks)
            {
                var operation = new DependencyListOperation(_options, framework);

                if (!operation.Execute())
                {
                    _options.Reports.Error.WriteLine("There was an error listing the dependencies");
                    return 3;
                }
            }

            return 0;
        }
    }
}