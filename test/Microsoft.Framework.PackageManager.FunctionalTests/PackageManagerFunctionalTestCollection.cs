// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Framework.PackageManager.FunctionalTests
{
    [CollectionDefinition(nameof(PackageManagerFunctionalTestCollection))]
    public class PackageManagerFunctionalTestCollection :
        ICollectionFixture<PackageManagerFunctionalTestFixture>
    {
    }
}
