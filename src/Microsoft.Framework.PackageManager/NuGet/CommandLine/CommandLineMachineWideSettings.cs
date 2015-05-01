// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet
{
    public class CommandLineMachineWideSettings : IMachineWideSettings
    {
        Lazy<IEnumerable<Settings>> _settings;

        public CommandLineMachineWideSettings()
        {
#if DNX451
            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
#else
            var baseDirectory = Environment.GetEnvironmentVariable("ProgramData");
#endif
            _settings = new Lazy<IEnumerable<NuGet.Settings>>(
                () => NuGet.Settings.LoadMachineWideSettings(
                    new PhysicalFileSystem(baseDirectory)));
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
