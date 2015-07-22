// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.Impl;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishManager
    {
        private readonly IServiceProvider _hostServices;
        private readonly PublishOptions _options;

        public PublishManager(IServiceProvider hostServices, PublishOptions options)
        {
            _hostServices = hostServices;
            _options = options;
            _options.ProjectDir = Normalize(_options.ProjectDir);

            var outputDir = _options.OutputDir ?? Path.Combine(_options.ProjectDir, "bin", "output");
            _options.OutputDir = Normalize(outputDir);
            ScriptExecutor = new ScriptExecutor(_options.Reports.Information);
        }

        public ScriptExecutor ScriptExecutor { get; private set; }

        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }

        public bool Publish()
        {
            var warnings = new List<DiagnosticMessage>();
            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(_options.ProjectDir, out project, warnings))
            {
                _options.Reports.Error.WriteLine("Unable to locate {0}.".Red(), Runtime.Project.ProjectFileName);
                return false;
            }

            foreach (var warning in warnings)
            {
                _options.Reports.Information.WriteLine(warning.FormattedMessage.Yellow());
            }

            // '--wwwroot' option can override 'webroot' property in project.json
            _options.WwwRoot = _options.WwwRoot ?? project.WebRoot;
            _options.WwwRootOut = _options.WwwRootOut ?? _options.WwwRoot;

            if (string.IsNullOrEmpty(_options.WwwRoot) && !string.IsNullOrEmpty(_options.WwwRootOut))
            {
                _options.Reports.Error.WriteLine(
                    "'--wwwroot-out' option can be used only when the '--wwwroot' option or 'webroot' in project.json is specified.".Red());
                return false;
            }

            if (string.Equals(_options.WwwRootOut, PublishRoot.AppRootName, StringComparison.OrdinalIgnoreCase))
            {
                _options.Reports.Error.WriteLine(
                    "'{0}' is a reserved folder name. Please choose another name for the wwwroot-out folder.".Red(),
                    PublishRoot.AppRootName);
                return false;
            }

            var sw = Stopwatch.StartNew();

            string outputPath = _options.OutputDir;

            var projectDir = project.ProjectDirectory;

            var frameworkContexts = new Dictionary<FrameworkName, DependencyContext>();

            var root = new PublishRoot(project, outputPath, _hostServices, _options.Reports)
            {
                Configuration = _options.Configuration,
                NoSource = _options.NoSource,
                IncludeSymbols = _options.IncludeSymbols
            };

            Func<string, string> getVariable = key =>
            {
                return null;
            };

            if (!ScriptExecutor.Execute(project, "prepare", getVariable))
            {
                _options.Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (!ScriptExecutor.Execute(project, "prepublish", getVariable))
            {
                _options.Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (!ResolveActualRuntimeNames(_options.Runtimes))
            {
                return false;
            }

            foreach (var runtime in _options.Runtimes)
            {
                // Calculate the runtime name by taking the last path segment (since it could be a path),
                // but first strip off any trailing '\' or '/' in case the path provided was something like
                //  "C:\Foo\Bar\dnx-clr-win-x64...\"
                var runtimeName = new DirectoryInfo(runtime).Name;
                var frameworkName = DependencyContext.SelectFrameworkNameForRuntime(
                    project.GetTargetFrameworks().Select(x => x.FrameworkName),
                    runtimeName);

                if (frameworkName == null)
                {
                    _options.Reports.Error.WriteLine(
                        "The project being published does not support the runtime '{0}'",
                        runtime.ToString().Red().Bold());
                    return false;
                }


                var runtimeLocated = TryAddRuntime(root, frameworkName, runtime);
                List<string> runtimeProbePaths = null;

                if (!runtimeLocated)
                {
                    runtimeProbePaths = new List<string>();
                    runtimeProbePaths.Add(runtime);
                    var runtimeHome = Environment.GetEnvironmentVariable(EnvironmentNames.Home);
                    if (string.IsNullOrEmpty(runtimeHome))
                    {
                        var runtimeGlobalPath = DnuEnvironment.GetFolderPath(DnuFolderPath.DnxGlobalPath);
                        var defaultRuntimeHome = DnuEnvironment.GetFolderPath(DnuFolderPath.DefaultDnxHome);
                        runtimeHome = $"{defaultRuntimeHome};{runtimeGlobalPath}";
                    }

                    foreach (var portion in runtimeHome.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var packagesPath = Path.Combine(
                            Environment.ExpandEnvironmentVariables(portion),
                            "runtimes",
                            runtime);

                        if (TryAddRuntime(root, frameworkName, packagesPath))
                        {
                            runtimeLocated = true;
                            break;
                        }

                        runtimeProbePaths.Add(packagesPath);
                    }
                }

                if (!runtimeLocated)
                {
                    _options.Reports.Error.WriteLine(string.Format("Unable to locate runtime '{0}'", runtime.Red().Bold()));
                    if (runtimeProbePaths != null)
                    {
                        _options.Reports.Error.WriteLine(string.Format("Locations probed:{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, runtimeProbePaths)));
                    }
                    return false;
                }

                if (!frameworkContexts.ContainsKey(frameworkName))
                {
                    frameworkContexts[frameworkName] = CreateDependencyContext(project, frameworkName);
                }
            }

            // If there is no target framework filter specified with '--runtime',
            // the published output targets all frameworks specified in project.json
            if (!_options.Runtimes.Any())
            {
                foreach (var frameworkInfo in project.GetTargetFrameworks())
                {
                    if (!frameworkContexts.ContainsKey(frameworkInfo.FrameworkName))
                    {
                        frameworkContexts[frameworkInfo.FrameworkName] =
                            CreateDependencyContext(project, frameworkInfo.FrameworkName);
                    }
                }
            }

            if (!frameworkContexts.Any())
            {
                // We can only get here if the project has no frameworks section, which we don't actually support any more
                _options.Reports.Error.WriteLine("The project being published has no frameworks listed in the 'frameworks' section.");
                return false;
            }

            root.SourcePackagesPath = frameworkContexts.First().Value.PackagesDirectory;

            bool anyUnresolvedDependency = false;
            foreach (var dependencyContext in frameworkContexts.Values)
            {
                // If there's any unresolved dependencies then complain and keep working
                if (dependencyContext.DependencyWalker.Libraries.Any(l => !l.Resolved))
                {
                    anyUnresolvedDependency = true;
                    var message = "Warning: " +
                        dependencyContext.DependencyWalker.GetMissingDependenciesWarning(
                            dependencyContext.FrameworkName);
                    _options.Reports.Quiet.WriteLine(message.Yellow());
                }

                foreach (var libraryDescription in dependencyContext.NuGetDependencyResolver.Dependencies)
                {
                    IList<DependencyContext> contexts;
                    if (!root.LibraryDependencyContexts.TryGetValue(libraryDescription.Identity, out contexts))
                    {
                        root.Packages.Add(new PublishPackage(libraryDescription));
                        contexts = new List<DependencyContext>();
                        root.LibraryDependencyContexts[libraryDescription.Identity] = contexts;
                    }
                    contexts.Add(dependencyContext);
                }

                foreach (var libraryDescription in dependencyContext.ProjectReferenceDependencyProvider.Dependencies)
                {
                    if (!root.Projects.Any(p => p.Name == libraryDescription.Identity.Name))
                    {
                        var publishProject = new PublishProject(
                            dependencyContext.ProjectReferenceDependencyProvider,
                            dependencyContext.ProjectResolver,
                            libraryDescription);

                        if (publishProject.Name == project.Name)
                        {
                            publishProject.WwwRoot = _options.WwwRoot;
                            publishProject.WwwRootOut = _options.WwwRootOut;
                        }
                        root.Projects.Add(publishProject);
                    }
                }
            }

            NativeImageGenerator nativeImageGenerator = null;
            if (_options.Native)
            {
                nativeImageGenerator = NativeImageGenerator.Create(_options, root, frameworkContexts.Values);
                if (nativeImageGenerator == null)
                {
                    _options.Reports.Error.WriteLine("Fail to initiate native image generation process.".Red());
                    return false;
                }
            }

            var success = root.Emit();

            if (!ScriptExecutor.Execute(project, "postpublish", getVariable))
            {
                _options.Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (_options.Native && !nativeImageGenerator.BuildNativeImages(root))
            {
                _options.Reports.Error.WriteLine("Native image generation failed.");
                return false;
            }

            sw.Stop();

            _options.Reports.Information.WriteLine("Time elapsed {0}", sw.Elapsed);
            return !anyUnresolvedDependency && success;
        }

        bool TryAddRuntime(PublishRoot root, FrameworkName frameworkName, string runtimePath)
        {
            if (!Directory.Exists(runtimePath))
            {
                return false;
            }

            root.Runtimes.Add(new PublishRuntime(root, frameworkName, runtimePath));
            return true;
        }

        private DependencyContext CreateDependencyContext(Runtime.Project project, FrameworkName frameworkName)
        {
            var dependencyContext = new DependencyContext(project.ProjectDirectory, _options.Configuration, frameworkName);
            dependencyContext.Walk(project.Name, project.Version);
            return dependencyContext;
        }

        private bool ResolveActualRuntimeNames(IList<string> runtimes)
        {
            for (int i = 0; i < runtimes.Count(); i++)
            {
                if (string.Equals("active", runtimes[i], StringComparison.OrdinalIgnoreCase))
                {
                    string activeRuntime = VersionUtils.ActiveRuntimeFullName;
                    if (string.IsNullOrEmpty(activeRuntime))
                    {
                        _options.Reports.Error.WriteLine("Cannot resolve the active runtime name".Red());
                        return false;
                    }

                    _options.Reports.Verbose.WriteLine("Resolved the active runtime as {0}", activeRuntime);
                    runtimes[i] = activeRuntime;
                }
            }

            return true;
        }
    }
}
