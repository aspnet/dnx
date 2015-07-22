// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.DesignTimeHost.Models;
using Microsoft.Dnx.Runtime;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class ProtocolManager
    {
        /// <summary>
        /// Environment variable for overriding protocol.
        /// </summary>
        public const string EnvDthProtocol = "DTH_PROTOCOL";

        /// <summary>
        /// Type name of the message representing the protocol negotiation.
        /// </summary>
        public const string NegotiationMessageTypeName = "ProtocolVersion";

        public ProtocolManager(int maxVersion)
        {
            MaxVersion = maxVersion;

            // initialized to the highest supported version or environment overridden value
            int? protocol = GetProtocolVersionFromEnvironment();

            if (protocol.HasValue)
            {
                CurrentVersion = protocol.Value;
                EnvironmentOverridden = true;
            }
            else
            {
                CurrentVersion = 1;
            }
        }

        public int MaxVersion { get; }

        public int CurrentVersion { get; private set; }

        public bool EnvironmentOverridden { get; }

        public bool IsProtocolNegotiation(Message message)
        {
            return message?.MessageType == NegotiationMessageTypeName;
        }

        public void Negotiate(Message message)
        {
            if (!IsProtocolNegotiation(message))
            {
                return;
            }

            Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Initializing the protocol negotiation.");

            if (EnvironmentOverridden)
            {
                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: DTH protocol negotiation is override by environment variable {EnvDthProtocol} and set to {CurrentVersion}.");
                return;
            }

            var tokenValue = message.Payload?["Version"];
            if (tokenValue == null)
            {
                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Protocol negotiation failed. Version property is missing in payload.");
                return;
            }

            var preferredVersion = tokenValue.Value<int>();
            if (preferredVersion == 0)
            {
                // the preferred version can't be zero. either property is missing or the the payload is corrupted.
                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Protocol negotiation failed. Protocol version 0 is invalid.");
                return;
            }

            CurrentVersion = Math.Min(preferredVersion, MaxVersion);
            Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Protocol negotiation successed. Use protocol {CurrentVersion}");

            if (message.Sender != null)
            {
                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Respond to protocol negotiation.");
                message.Sender.Transmit(new Message
                {
                    ContextId = 0,
                    MessageType = NegotiationMessageTypeName,
                    Payload = JToken.FromObject(new { Version = CurrentVersion })
                });
            }
            else
            {
                Logger.TraceWarning($"[{nameof(ProtocolManager)}]: {nameof(Message.Sender)} is null.");
            }
        }

        private static int? GetProtocolVersionFromEnvironment()
        {
            // look for the environment variable DTH_PROTOCOL, if it is set override the protocol version.
            // this is for debugging.
            var strProtocol = Environment.GetEnvironmentVariable(EnvDthProtocol);
            int intProtocol = -1;
            if (!string.IsNullOrEmpty(strProtocol) && Int32.TryParse(strProtocol, out intProtocol))
            {
                return intProtocol;
            }

            return null;
        }
    }
}
