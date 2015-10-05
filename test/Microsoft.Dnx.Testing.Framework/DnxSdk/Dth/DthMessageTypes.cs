// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Testing.Framework.DesignTimeHost
{
    public class DthMessageTypes
    {
        // requests
        public const string GetDiagnostics = nameof(GetDiagnostics);
        public const string ProtocolVersion = nameof(ProtocolVersion);
        public const string ChangeConfiguration = nameof(ChangeConfiguration);

        // responses
        public const string AllDiagnostics = nameof(AllDiagnostics);
    }
}
