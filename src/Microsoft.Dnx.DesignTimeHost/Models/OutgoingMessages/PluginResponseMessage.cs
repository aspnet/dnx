// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages
{
    public class PluginResponseMessage
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string MessageId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string MessageName { get; set; }

        public bool Success
        {
            get
            {
                return Error == null;
            }
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }
    }
}