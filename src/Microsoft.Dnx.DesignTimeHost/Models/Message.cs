// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost.Models
{
    public class Message
    {
        public string HostId { get; set; }

        public string MessageType { get; set; }

        public int ContextId { get; set; } = -1;

        public JToken Payload { get; set; }

        [JsonIgnore]
        public ConnectionContext Sender { get; set; }

        public override string ToString()
        {
            return "(" + HostId + ", " + MessageType + ", " + ContextId + ") -> " + (Payload == null ? "null" : Payload.ToString(Formatting.Indented));
        }
    }
}