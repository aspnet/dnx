// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IAssemblyLoaderEngine
    {
        Assembly LoadFile(string path);
        Assembly LoadStream(Stream assemblyStream, Stream pdbStream);
    }
}
