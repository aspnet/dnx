// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Dnx.Tooling;

namespace NuGet
{
    public class CommandLineMachineWideSettings : IMachineWideSettings
    {
        Lazy<IEnumerable<Settings>> _settings;

        public CommandLineMachineWideSettings()
        {
            string machineWideConfigDir = DnuEnvironment.GetFolderPath(DnuFolderPath.MachineWideConfigDirectory);

            _settings = new Lazy<IEnumerable<NuGet.Settings>>(
                () => NuGet.Settings.LoadMachineWideSettings(
                    new PhysicalFileSystem(machineWideConfigDir)));
        }

        public IEnumerable<Settings> Settings
        {
            get
            {
                return _settings.Value;
            }
        }
    }
}
