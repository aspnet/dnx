// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Dnx.Tooling.Algorithms
{
    public interface IGraphNode<TValue>
    {
        TValue Item { get; }

        IEnumerable<IGraphNode<TValue>> Children { get; }
    }
}