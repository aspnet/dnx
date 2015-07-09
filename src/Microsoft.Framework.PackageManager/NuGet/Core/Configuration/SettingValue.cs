// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet
{
    public class SettingValue
    {
        public SettingValue(string key, string value, bool isMachineWide, ISettings origin)
        {
            Key = key;
            Value = value;
            IsMachineWide = isMachineWide;
            Origin = origin;
        }

        public string Key { get; private set; }

        public string Value { get; set; }

        public ISettings Origin { get; set; }

        public bool IsMachineWide { get; private set; }

        public override bool Equals(object obj)
        {
            var rhs = obj as SettingValue;
            if (rhs == null)
            {
                return false;
            }

            return rhs.Key == Key && 
                rhs.Value == Value && 
                rhs.IsMachineWide == rhs.IsMachineWide;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(Key, Value, IsMachineWide).GetHashCode();
        }
    }
}
