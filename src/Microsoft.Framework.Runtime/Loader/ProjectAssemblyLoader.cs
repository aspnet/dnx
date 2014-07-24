// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
{
    internal class ProjectAssemblyLoader : IAssemblyLoader
    {
        private readonly IProjectResolver _projectResolver;
        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private readonly IApplicationEnvironment _applicationEnvironment;

        public ProjectAssemblyLoader(IProjectResolver projectResovler,
                                     IAssemblyLoaderEngine loaderEngine,
                                     IApplicationEnvironment applicationEnvironment,
                                     ILibraryExportProvider libraryExportProvider)
        {
            _projectResolver = projectResovler;
            _loaderEngine = loaderEngine;
            _applicationEnvironment = applicationEnvironment;
            _libraryExportProvider = libraryExportProvider;
        }

        public Assembly Load(string name)
        {
            Project project;
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            var export = _libraryExportProvider.GetLibraryExport(name,
                                                                 _applicationEnvironment.TargetFramework,
                                                                 _applicationEnvironment.Configuration);

            if (export == null)
            {
                return null;
            }

            foreach (var projectReference in export.MetadataReferences.OfType<IMetadataProjectReference>())
            {
                if (string.Equals(projectReference.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return LoadAssembly(projectReference);
                }
            }

            return null;
        }

        private Assembly LoadAssembly(IMetadataProjectReference projectReference)
        {
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
                IProjectBuildResult result = projectReference.EmitAssembly(assemblyStream, pdbStream);

                if (!result.Success)
                {
                    throw new CompilationException(result.Errors.ToList());
                }

                Assembly assembly = null;

                // Rewind the stream
                assemblyStream.Seek(0, SeekOrigin.Begin);

                if (PlatformHelper.IsMono)
                {
                    // Pdbs aren't supported on mono
                    assembly = _loaderEngine.LoadStream(assemblyStream, pdbStream: null);
                }
                else
                {
                    // Rewind the pdb stream
                    pdbStream.Seek(0, SeekOrigin.Begin);

                    assembly = _loaderEngine.LoadStream(assemblyStream, pdbStream);
                }

                return assembly;
            }
        }
    }
}
