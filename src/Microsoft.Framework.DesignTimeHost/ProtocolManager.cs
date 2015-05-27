// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost
{
    public class ProtocolManager
    {
        /// <summary>
        /// Supported protocol versions in descending order.
        /// </summary>
        public static readonly int[] SupportedProtocolVersions = new int[] { 2 };

        /// <summary>
        /// Environment variable for overriding protocol.
        /// </summary>
        public const string EnvDthProtocol = "DTH_PROTOCOL";

        /// <summary>
        /// Type name of the message representing the protocol negotiation.
        /// </summary>
        public const string NegotiationMessageTypeName = "ProtocolVersion";

        /// <summary>
        /// Type name of the message representing the protocol negotiation.
        /// </summary>
        public const string InitializeMessageTypeName = "Initialize";

        private readonly bool _environmentOverriden;

        public ProtocolManager()
        {
            // initialized to the highest supported version or environment overriden value
            int? protocol = GetProtocolVersionFromEnvironment();
            
            if (protocol.HasValue)
            {
                CurrentVersion = protocol.Value;
                _environmentOverriden = true;
            }
            else
            {
                CurrentVersion = SupportedProtocolVersions[0];
                _environmentOverriden = false;
            }
        }

        public int CurrentVersion { get; private set; }

        public bool NeedToRespond { get; private set; } = false;

        public void Negotiate(Message message)
        {
            Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Initialize the protocol negotication.");

            if (_environmentOverriden)
            {
                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: DTH protocol negotiation is override by environment variable {EnvDthProtocol} and set to {CurrentVersion}.");
                return;
            }

            if (message.MessageType == NegotiationMessageTypeName)
            {
                if (message.ContextId != 0)
                {
                    return;
                }

                InternalNegotiate(message, needRespond: true);
            }
            else if (message.MessageType == InitializeMessageTypeName)
            {
                InternalNegotiate(message, needRespond: false);
            }
        }

        private void InternalNegotiate(Message message, bool needRespond = false)
        {
            var tokenValue = message.Payload?["Version"];
            if (tokenValue == null)
            {
                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Protocol negotication failed. Version property is missing in payload.");
                return;
            }

            var preferredVersion = tokenValue.Value<int>();
            if (preferredVersion == 0)
            {
                // the preferred version can't be zero. either property is missing or the the payload is corrupted.
                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Protocol negotication failed. Protocol version 0 is invalid.");
                return;
            }

            int? protocol = null;
            for (int i = 0; i < SupportedProtocolVersions.Length; ++i)
            {
                if (SupportedProtocolVersions[i] <= preferredVersion)
                {
                    protocol = SupportedProtocolVersions[i];
                    break;
                }
            }

            if (protocol.HasValue)
            {
                CurrentVersion = protocol.Value;
                NeedToRespond = needRespond;

                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Protocol negotication successed. Use protocol {CurrentVersion}");
            }
            else
            {
                Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Protocol negotication failed. Stay on protocol {CurrentVersion}");
            }
        }

        public void Respond(ConnectionContext context)
        {
            Logger.TraceInformation($"[{nameof(ProtocolManager)}]: Respond to protocol negotiation.");

            context.Transmit(new Message
            {
                ContextId = 0,
                MessageType = NegotiationMessageTypeName,
                Payload = JToken.FromObject(new { Version = CurrentVersion })
            });
            
            NeedToRespond = false;
        }

        private static int? GetProtocolVersionFromEnvironment()
        {
            // look for the environment variable DTH_PROTOCOL, if it is set override the protocol version.
            // this is for debugging.
            var strProtocol = Environment.GetEnvironmentVariable("DTH_PROTOCOL");
            int intProtocol = -1;
            if (!string.IsNullOrEmpty(strProtocol) && Int32.TryParse(strProtocol, out intProtocol))
            {
                return intProtocol;
            }

            return null;
        }
    }
}
