// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public interface IProjectExportProvider
    {
        ILibraryExport GetProjectExport(Project project, FrameworkName targetFramework, string configuration, ILibraryExport projectExport);
    }
}