// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public interface IRuntimeEnvironment
    {
        string OperatingSystem { get; }

        string OperatingSystemVersion { get; }

        string RuntimeType { get; }

        string RuntimeArchitecture { get; }

        string RuntimeVersion { get; }
    }
}
