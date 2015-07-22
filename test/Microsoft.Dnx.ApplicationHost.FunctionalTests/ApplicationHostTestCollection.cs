// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.CommonTestUtils;
using Xunit;

namespace Microsoft.Dnx.ApplicationHost.FunctionalTests
{
    [CollectionDefinition("ApplicationHostTestCollection")]
    public class ApplicationHostTestCollection: ICollectionFixture<DnxRuntimeFixture>
    {
    }
}
