// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost.Models.IncomingMessages
{
    public class PluginMessage
    {
        public string PluginId { get; set; }
        public string MessageId { get; set; }
        public string MessageName { get; set; }
        public JObject Data { get; set; }
    }
}