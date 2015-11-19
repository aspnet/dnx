// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet
{
    public class NullSettings : ISettings
    {
        private static readonly NullSettings _settings = new NullSettings();

        public static NullSettings Instance
        {
            get { return _settings; }
        }

        public string GetValue(string section, string key)
        {
            return String.Empty;
        }

        public string GetValue(string section, string key, bool isPath)
        {
            return String.Empty;
        }

        public IList<KeyValuePair<string, string>> GetValues(string section)
        {
            return new List<KeyValuePair<string, string>>().AsReadOnly();
        }

        public IList<KeyValuePair<string, string>> GetValues(string section, bool isPath)
        {
            return new List<KeyValuePair<string, string>>().AsReadOnly();
        }

        public IList<SettingValue> GetSettingValues(string section, bool isPath)
        {
            return new List<SettingValue>().AsReadOnly();
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string key)
        {
            return new List<KeyValuePair<string, string>>().AsReadOnly();
        }
    }
}
