// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.FileSystem;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynProjectMetadataProvider : IProjectMetadataProvider
    {
        private readonly RoslynCompiler _compiler;

        public RoslynProjectMetadataProvider(IProjectResolver projectResolver, 
                                             ILibraryExportProvider libraryExportProvider)
        {
            _compiler = new RoslynCompiler(projectResolver,
                                           NoopWatcher.Instance,
                                           libraryExportProvider);
        }

        public IProjectMetadata GetProjectMetadata(string name, FrameworkName targetFramework, string configuration)
        {
            var context = _compiler.CompileProject(name, targetFramework, configuration);

            if (context == null)
            {
                return null;
            }

            return new RoslynProjectMetadata(context);
        }
    }
}
