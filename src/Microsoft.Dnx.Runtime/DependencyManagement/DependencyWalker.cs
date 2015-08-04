// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using NuGet;

namespace Microsoft.Dnx.Runtime
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

        public IList<DiagnosticMessage> GetDependencyDiagnostics(string projectFilePath)
        {
            var messages = new List<DiagnosticMessage>();
            foreach (var library in Libraries)
            {
                string projectPath = library.LibraryRange.FileName ?? projectFilePath;

                if (!library.Resolved)
                {
                    messages.Add(
                        new DiagnosticMessage(
                            $"The dependency {library.LibraryRange} could not be resolved.", 
                            projectPath, 
                            DiagnosticMessageSeverity.Error, 
                            library.LibraryRange.Line, 
                            library.LibraryRange.Column));
                }
                else if (!library.Compatible)
                {
                    messages.Add(
                        new DiagnosticMessage(
                            library.CompatibilityIssue.ToString(),
                            projectPath, 
                            DiagnosticMessageSeverity.Error, 
                            library.LibraryRange.Line, 
                            library.LibraryRange.Column));
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
                        messages.Add(
                            new DiagnosticMessage(
                                message, 
                                projectPath, 
                                DiagnosticMessageSeverity.Warning, 
                                library.LibraryRange.Line, 
                                library.LibraryRange.Column));
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

            return sb.ToString();
        }

        private IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return DependencyProviders.SelectMany(p => p.GetAttemptedPaths(targetFramework));
        }
    }
}
