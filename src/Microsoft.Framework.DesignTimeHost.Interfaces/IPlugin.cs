// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost
{
    public interface IPlugin
    {
        void ProcessMessage(JObject data);
    }
}