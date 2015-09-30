// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.CommonTestUtils;
using Xunit;

namespace Bootstrapper.FunctionalTests
{
    [CollectionDefinition("BootstrapperTestCollection")]
    public class BootstrapperTestCollection : ICollectionFixture<DnxRuntimeFixture>
    {
    }
}
