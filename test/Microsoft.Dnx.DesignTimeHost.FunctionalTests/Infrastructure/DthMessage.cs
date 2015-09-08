// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure
{
    public class DthMessage
    {
        public string HostId { get; set; }

        public string MessageType { get; set; }

        public int ContextId { get; set; }

        public int Version { get; set; }

        public JToken Payload { get; set; }

        /// <summary>
        /// Throws if the message is not generated in communication between given server and client
        /// </summary>
        public void EnsureSource(DthTestServer server, DthTestClient client)
        {
            if (HostId != server.HostId)
            {
                throw new Exception($"{nameof(HostId)} doesn't match the one of server. Expected {server.HostId} but actually {HostId}.");
            }

            if (ContextId != client.ContextId)
            {
                throw new Exception($"{nameof(ContextId)} doesn't match the one of client. Expected {client.ContextId} but actually {ContextId}.");
            }
        }
    }
}
