// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public static class JTokenExtensions
    {
        public static T[] ValueAsArray<T>(this JToken jToken)
        {
            return jToken.Select(a => a.Value<T>()).ToArray();
        }

        public static T[] ValueAsArray<T>(this JToken jToken, string name)
        {
            return jToken?[name]?.ValueAsArray<T>();
        }
    }
}