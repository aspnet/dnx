// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Tooling
{
    internal class ListCommandsCommand
    {
        private readonly Reports _reports;

        public ListCommandsCommand(Reports reports)
        {
            _reports = reports;
        }

        public bool Execute()
        {
            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(".", out project))
            {
                _reports.WriteError("Could not find project.json");
                return false;
            }

            _reports.Information.WriteLine("Available commands:");

            foreach (var command in project.Commands)
            {
                var line = $"    {command.Key.Bold()} : {command.Value}";
                _reports.Information.WriteLine(line);
            }

            return true;
        }
    }
}
