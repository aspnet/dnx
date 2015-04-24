// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Configuration;

namespace Microsoft.Framework.PackageManager
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
            _settings = new Lazy<IEnumerable<Settings>>(
                () => NuGet.Configuration.Settings.LoadMachineWideSettings(baseDirectory));
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
