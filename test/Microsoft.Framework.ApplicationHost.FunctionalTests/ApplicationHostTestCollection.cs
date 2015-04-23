// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.CommonTestUtils;
using Xunit;

namespace Microsoft.Framework.ApplicationHost.FunctionalTests
{
    [CollectionDefinition("ApplicationHostTestCollection")]
    public class ApplicationHostTestCollection: ICollectionFixture<DnxRuntimeFixture>
    {
    }
}
