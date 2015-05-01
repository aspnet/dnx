// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.IO;

namespace NuGet
{
    public interface IPackageBuilder : IPackageMetadata
    {
        Collection<IPackageFile> Files { get; }
        void Save(Stream stream);
    }
}
