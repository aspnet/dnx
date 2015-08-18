// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure
{
    public class DthMessage<T>
    {
        public string HostId { get; set; }

        public string MessageType { get; set; }

        public int ContextId { get; set; }

        public int Version { get; set; }

        public T Payload { get; set; }
    }
}
