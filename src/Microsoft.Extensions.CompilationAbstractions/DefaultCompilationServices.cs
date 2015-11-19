// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Extensions.CompilationAbstractions
{
    internal class DefaultCompilationServices : CompilationServices
    {
        internal DefaultCompilationServices(ILibraryExporter libraryExporter, ICompilerOptionsProvider compilerOptionsProvider)
        {
            LibraryExporter = libraryExporter;
            CompilerOptionsProvider = compilerOptionsProvider;
        }

        public override ILibraryExporter LibraryExporter { get; }
        public override ICompilerOptionsProvider CompilerOptionsProvider { get; }
    }
}