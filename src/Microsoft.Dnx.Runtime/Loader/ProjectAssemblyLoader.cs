// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Linq;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class ProjectAssemblyLoader : IAssemblyLoader
    {
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private readonly ICompilationEngine _compilationEngine;
        private readonly string _configuration;
        private readonly IDictionary<string, RuntimeProject> _projects;
        private readonly ConcurrentDictionary<string, byte> _unloadableNativeLibs = new ConcurrentDictionary<string, byte>();

        public ProjectAssemblyLoader(IAssemblyLoadContextAccessor loadContextAccessor,
                                     ICompilationEngine compilationEngine,
                                     IEnumerable<ProjectDescription> projects,
                                     string configuration)
        {
            _loadContextAccessor = loadContextAccessor;
            _compilationEngine = compilationEngine;
            _projects = projects.ToDictionary(p => p.Identity.Name, p => new RuntimeProject(p.Project, p.Framework));
            _configuration = configuration;

            var environment = RuntimeEnvironmentHelper.RuntimeEnvironment;
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName, _loadContextAccessor.Default);
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            // An assembly name like "MyLibrary!alternate!more-text"
            // is parsed into:
            // name == "MyLibrary"
            // aspect == "alternate"
            // and the more-text may be used to force a recompilation of an aspect that would
            // otherwise have been cached by some layer within Assembly.Load
            var name = assemblyName.Name;
            string aspect = null;
            var parts = name.Split(new[] { '!' }, 3);
            if (parts.Length != 1)
            {
                name = parts[0];
                aspect = parts[1];
            }

            if (!string.IsNullOrEmpty(assemblyName.CultureName) &&
                Path.GetExtension(name).Equals(".resources", StringComparison.OrdinalIgnoreCase))
            {
                name = Path.GetFileNameWithoutExtension(name);
            }

            RuntimeProject project;
            if (!_projects.TryGetValue(name, out project))
            {
                return null;
            }

            return _compilationEngine.LoadProject(
                project.Project,
                project.Framework,
                aspect,
                loadContext,
                assemblyName,
                _configuration);
        }

        private struct RuntimeProject
        {
            public RuntimeProject(Project project, FrameworkName targetFramework)
            {
                Project = project;
                Framework = targetFramework;
            }

            public Project Project { get; }
            public FrameworkName Framework { get; }
        }

        public IntPtr LoadUnmanagedLibrary(string name)
        {
            if (_unloadableNativeLibs.ContainsKey(name))
            {
                return IntPtr.Zero;
            }

            foreach(var projectPath in _projects.Values.Select(p => p.Project.ProjectDirectory))
            {
                foreach (var folder in NativeLibPathUtils.GetNativeSubfolderCandidates(RuntimeEnvironmentHelper.RuntimeEnvironment))
                {
                    var path = NativeLibPathUtils.GetProjectNativeLibPath(projectPath, folder);
                    if (Directory.Exists(path))
                    {
                        var handle = LoadUnamangedLibrary(path, name);
                        if (handle != IntPtr.Zero)
                        {
                            return handle;
                        }
                    }
                }
            }

            _unloadableNativeLibs.TryAdd(name, 0);

            return IntPtr.Zero;
        }

        private IntPtr LoadUnamangedLibrary(string path, string name)
        {
            foreach (var nativeLibFullPath in Directory.EnumerateFiles(path))
            {
                if (NativeLibPathUtils.IsMatchingNativeLibrary(RuntimeEnvironmentHelper.RuntimeEnvironment, name, Path.GetFileName(nativeLibFullPath)))
                {
                    return _loadContextAccessor.Default.LoadUnmanagedLibraryFromPath(nativeLibFullPath);
                }
            }

            return IntPtr.Zero;
        }

        private string ExpectedExtension { get; }
    }
}