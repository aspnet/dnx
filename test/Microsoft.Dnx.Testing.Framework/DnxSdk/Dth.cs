// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Testing.Framework.DesignTimeHost;

namespace Microsoft.Dnx.Testing.Framework
{
    public class Dth
    {
        private readonly string _sdkPath;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);

        public Dth(string sdkPath)
        {
            _sdkPath = sdkPath;
        }

        public DthTestServer CreateServer()
        {
            return CreateServer(_defaultTimeout);
        }

        public DthTestServer CreateServer(TimeSpan timeout)
        {
            return DthTestServer.Create(_sdkPath, timeout);
        }
    }
}
