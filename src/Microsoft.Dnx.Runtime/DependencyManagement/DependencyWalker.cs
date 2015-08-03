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
                string projectPath = library.RequestedRange.FileName ?? projectFilePath;

                if (!library.Resolved)
                {
                    string message;
                    if (library.Compatible)
                    {
                        message = $"The dependency {library.RequestedRange} could not be resolved.";
                    }
                    else
                    {
                        var projectName = Directory.GetParent(projectFilePath).Name;
                        message =
                            $"The dependency {library.Identity} in project {projectName} does not support framework {library.Framework}.";
                    }

                    messages.Add(
                        new DiagnosticMessage(
                            message, 
                            projectPath, 
                            DiagnosticMessageSeverity.Error, 
                            library.RequestedRange.Line, 
                            library.RequestedRange.Column));
                }
                else
                {
                    // Skip libraries that aren't specified in a project.json
                    if (string.IsNullOrEmpty(library.RequestedRange.FileName))
                    {
                        continue;
                    }

                    if (library.RequestedRange.VersionRange == null)
                    {
                        // TODO: Show errors/warnings for things without versions
                        continue;
                    }

                    // If we ended up with a declared version that isn't what was asked for directly
                    // then report a warning
                    // Case 1: Non floating version and the minimum doesn't match what was specified
                    // Case 2: Floating version that fell outside of the range
                    if ((library.RequestedRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.None &&
                         library.RequestedRange.VersionRange.MinVersion != library.Identity.Version) ||
                        (library.RequestedRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None &&
                         !library.RequestedRange.VersionRange.EqualsFloating(library.Identity.Version)))
                    {
                        var message = string.Format("Dependency specified was {0} but ended up with {1}.", library.RequestedRange, library.Identity);
                        messages.Add(
                            new DiagnosticMessage(
                                message, 
                                projectPath, 
                                DiagnosticMessageSeverity.Warning, 
                                library.RequestedRange.Line, 
                                library.RequestedRange.Column));
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
                sb.Append("   " + d.Identity);
                if (!d.Compatible)
                {
                    sb.Append(" (incompatible)");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return DependencyProviders.SelectMany(p => p.GetAttemptedPaths(targetFramework));
        }
    }
}
