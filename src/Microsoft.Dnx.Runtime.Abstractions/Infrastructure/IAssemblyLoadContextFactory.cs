// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Runtime.Infrastructure
{
    public interface IAssemblyLoadContextFactory
    {
        IAssemblyLoadContext Create(IServiceProvider serviceProvider);
    }
}