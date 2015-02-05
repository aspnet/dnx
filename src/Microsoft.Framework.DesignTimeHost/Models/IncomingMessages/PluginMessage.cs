// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.IncomingMessages
{
    public class PluginMessage
    {
        public string PluginId { get; set; }
        public string MessageName { get; set; }
        public JObject Data { get; set; }
    }
}