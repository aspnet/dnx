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
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is the best fit for this internal class")]
        IList<KeyValuePair<string, string>> GetValues(string section);

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is the best fit for this internal class")]
        IList<KeyValuePair<string, string>> GetValues(string section, bool isPath);

        IList<SettingValue> GetSettingValues(string section, bool isPath);

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is the best fit for this internal class")]
        IList<KeyValuePair<string, string>> GetNestedValues(string section, string key);
        
        void SetValue(string section, string key, string value);
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is the best fit for this internal class")]
        void SetValues(string section, IList<KeyValuePair<string, string>> values);
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is the best fit for this internal class")]
        void SetNestedValues(string section, string key, IList<KeyValuePair<string, string>> values);
        
        bool DeleteValue(string section, string key);
        bool DeleteSection(string section);
    }
}
