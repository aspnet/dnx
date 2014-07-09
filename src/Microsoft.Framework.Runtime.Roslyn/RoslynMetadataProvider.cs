// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Loader;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynMetadataProvider
    {
        private readonly IRoslynCompiler _compiler;

        public RoslynMetadataProvider(IRoslynCompiler compiler)
        {
            _compiler = compiler;
        }

        public RoslynProjectMetadata GetMetadata(string name, FrameworkName targetFramework, string configuration)
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
