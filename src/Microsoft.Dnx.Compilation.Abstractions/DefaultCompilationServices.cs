// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Compilation
{
    internal class DefaultCompilationServices : CompilationServices
    {
        internal DefaultCompilationServices(ILibraryExporter libraryExporter)
        {
            LibraryExporter = libraryExporter;
        }

        public override ILibraryExporter LibraryExporter { get; }
    }
}