// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime
{
    internal static class DiagnosticsUtils
    {
        public static List<ICompilationMessage> GetAllReferenceProjectDiagnostics(ILibraryManager libraryManager,
            string projectName)
        {
            var projectExport = libraryManager.GetAllExports(projectName);
            var metadataProjectRefs = projectExport.MetadataReferences.OfType<IMetadataProjectReference>();
            return metadataProjectRefs.SelectMany(x => x.GetDiagnostics().Diagnostics).ToList();
        }
    }
}