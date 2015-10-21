// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Compilation
{
    public abstract class CompilationServices
    {
        private static CompilationServices _defaultCompilationServices;

        public static CompilationServices Default
        {
            get
            {
                if (_defaultCompilationServices == null)
                {
                    throw new InvalidOperationException("Trying to get CompilationServices Default before it was set");
                }
                return _defaultCompilationServices;
            }
        }
        public abstract ILibraryExporter LibraryExporter { get; }

        public abstract ICompilerOptionsProvider CompilerOptionsProvider { get; }

        public static void SetDefault(CompilationServices compilationServices)
        {
            _defaultCompilationServices = compilationServices;
        }

        public static CompilationServices Create(ILibraryExporter libraryExporter, ICompilerOptionsProvider compilerOptionsProvider)
        {
            return new DefaultCompilationServices(libraryExporter, compilerOptionsProvider);
        }

    }
}
