// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Framework.Runtime.Infrastructure;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class DependencyWalker
    {
        private readonly IEnumerable<IDependencyProvider> _dependencyProviders;
        private readonly List<LibraryDescription> _libraries = new List<LibraryDescription>();

        public DependencyWalker(IEnumerable<IDependencyProvider> dependencyProviders)
        {
            _dependencyProviders = dependencyProviders;
        }

        public IList<LibraryDescription> Libraries
        {
            get { return _libraries; }
        }

        public IEnumerable<IDependencyProvider> DependencyProviders
        {
            get
            {
                return _dependencyProviders;
            }
        }

        public void Walk(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            var sw = Stopwatch.StartNew();
            Logger.TraceInformation("[{0}]: Walking dependency graph for '{1} {2}'.", GetType().Name, name, targetFramework);

            var context = new WalkContext();

            var walkSw = Stopwatch.StartNew();

            context.Walk(
                _dependencyProviders,
                name,
                version,
                targetFramework);

            walkSw.Stop();
            Logger.TraceInformation("[{0}]: Graph walk took {1}ms.", GetType().Name, walkSw.ElapsedMilliseconds);

            context.Populate(targetFramework, Libraries);

            sw.Stop();
            Logger.TraceInformation("[{0}]: Resolved dependencies for {1} in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);
        }

        public IList<ICompilationMessage> GetDependencyDiagnostics(string projectFilePath)
        {
            var messages = new List<ICompilationMessage>();

            messages.AddRange(DependencyProviders.SelectMany(x => x.GetDiagnostics()));

            foreach (var library in Libraries)
            {
                string projectPath = library.LibraryRange.FileName ?? projectFilePath;

                if (!library.Resolved)
                {
                    var message = string.Format("Dependency {0} could not be resolved", library.LibraryRange);

                    messages.Add(new FileFormatMessage(message, projectPath, CompilationMessageSeverity.Error)
                    {
                        StartLine = library.LibraryRange.Line,
                        StartColumn = library.LibraryRange.Column
                    });
                }
                else
                {
                    // Skip libraries that aren't specified in a project.json
                    if (string.IsNullOrEmpty(library.LibraryRange.FileName))
                    {
                        continue;
                    }

                    if (library.LibraryRange.VersionRange == null)
                    {
                        // TODO: Show errors/warnings for things without versions
                        continue;
                    }

                    // If we ended up with a declared version that isn't what was asked for directly
                    // then report a warning
                    // Case 1: Non floating version and the minimum doesn't match what was specified
                    // Case 2: Floating version that fell outside of the range
                    if ((library.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.None &&
                         library.LibraryRange.VersionRange.MinVersion != library.Identity.Version) ||
                        (library.LibraryRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None &&
                         !library.LibraryRange.VersionRange.EqualsFloating(library.Identity.Version)))
                    {
                        var message = string.Format("Dependency specified was {0} but ended up with {1}.", library.LibraryRange, library.Identity);
                        messages.Add(new FileFormatMessage(message, projectPath, CompilationMessageSeverity.Warning)
                        {
                            StartLine = library.LibraryRange.Line,
                            StartColumn = library.LibraryRange.Column
                        });
                    }
                }
            }

            return messages;
        }

        public string GetMissingDependenciesWarning(FrameworkName targetFramework)
        {
            var sb = new StringBuilder();

            // TODO: Localize messages

            sb.AppendFormat("Failed to resolve the following dependencies for target framework '{0}':", targetFramework.ToString());
            sb.AppendLine();

            foreach (var d in Libraries.Where(d => !d.Resolved).OrderBy(d => d.Identity.Name))
            {
                sb.AppendLine("   " + d.Identity.ToString());
            }

            foreach (var diagnostic in DependencyProviders.SelectMany(x => x.GetDiagnostics()))
            {
                sb.AppendLine();
                sb.AppendLine(diagnostic.Message);
            }

            sb.AppendLine();

            // TODO: make this RuntimeEnvironmentHelper.GetPrettyPrintRuntimeInfo() after https://github.com/aspnet/dnx/pull/1692 is checked in
            var runtimeEnv = CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof(IRuntimeEnvironment)) as IRuntimeEnvironment;
            var shortName = VersionUtility.GetShortFrameworkName(targetFramework);
            var runtimeInfo = $@"Current runtime Target Framework: '{targetFramework} ({shortName})'
  Type: {runtimeEnv.RuntimeType}
  Architecture: {runtimeEnv.RuntimeArchitecture}
  Version: {runtimeEnv.RuntimeVersion}";
            sb.AppendLine(runtimeInfo);

            return sb.ToString();
        }

        private IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return DependencyProviders.SelectMany(p => p.GetAttemptedPaths(targetFramework));
        }
    }
}
