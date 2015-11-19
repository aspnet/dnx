// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NuGet
{
    public interface ISettings
    {
        string GetValue(string section, string key);
        string GetValue(string section, string key, bool isPath);
        IList<KeyValuePair<string, string>> GetValues(string section);

        IList<KeyValuePair<string, string>> GetValues(string section, bool isPath);

        IList<SettingValue> GetSettingValues(string section, bool isPath);

        IList<KeyValuePair<string, string>> GetNestedValues(string section, string key);
    }
}
