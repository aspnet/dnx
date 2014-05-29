// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public static class ProjectExtensions
    {
        public static CompilationSettings GetCompilationSettings(this Project project, string configurationName)
        {
            return GetCompilationSettings(project, Project.ParseFrameworkName(configurationName));
        }

        public static CompilationSettings GetCompilationSettings(this Project project, FrameworkName targetFramework)
        {
            var rootOptions = project.GetCompilerOptions();
            var rootDefines = rootOptions.Defines ?? Enumerable.Empty<string>();
            var languageVersionValue = rootOptions.LanguageVersion;

            var specificOptions = project.GetCompilerOptions(targetFramework);
            var specificDefines = (specificOptions == null ? null : specificOptions.Defines) ?? new[] {
                MakeDefaultTargetFrameworkDefine(targetFramework)
            };

            var defaultOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            var options = GetCompilationOptions(specificOptions) ??
                          GetCompilationOptions(rootOptions) ??
                          defaultOptions;

            // Disable 1702 until roslyn turns this off by default
            options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                { "CS1702", ReportDiagnostic.Suppress }
            });

            // TODO: Base this on debug/release configuration (when we
            // support it)
            options = options.WithDebugInformationKind(DebugInformationKind.Full);

            if (PlatformHelper.IsMono)
            {
                options = options.WithConcurrentBuild(concurrentBuild: false);
            }

            var assemblyIdentityComparer = VersionUtility.IsDesktop(targetFramework) ?
                DesktopAssemblyIdentityComparer.Default : null;

            options = options.WithAssemblyIdentityComparer(assemblyIdentityComparer);

            LanguageVersion languageVersion;
            if (!Enum.TryParse<LanguageVersion>(value: languageVersionValue,
                                                ignoreCase: true,
                                                result: out languageVersion))
            {
                languageVersion = LanguageVersion.CSharp6;
            }

            var settings = new CompilationSettings
            {
                LanguageVersion = languageVersion,
                Defines = rootDefines.Concat(specificDefines).ToArray(),
                CompilationOptions = options
            };

            return settings;
        }

        private static string MakeDefaultTargetFrameworkDefine(FrameworkName targetFramework)
        {
            var shortName = VersionUtility.GetShortFrameworkName(targetFramework);

            if (VersionUtility.IsPortableFramework(targetFramework))
            {
                return shortName.Substring("portable-".Length).Replace('+', '_');
            }

            return shortName.ToUpperInvariant();
        }

        private static CSharpCompilationOptions GetCompilationOptions(CompilerOptions compilerOptions)
        {
            if (compilerOptions == null)
            {
                return null;
            }

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                                .WithHighEntropyVirtualAddressSpace(true);

            bool allowUnsafe = compilerOptions.AllowUnsafe;
            string platformValue = compilerOptions.Platform;
            bool warningsAsErrors = compilerOptions.WarningsAsErrors;

            Platform platform;
            if (!Enum.TryParse<Platform>(value: platformValue,
                                         ignoreCase: true,
                                         result: out platform))
            {
                platform = Platform.AnyCpu;
            }

            ReportDiagnostic warningOption = warningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default;

            return options.WithAllowUnsafe(allowUnsafe)
                          .WithPlatform(platform)
                          .WithGeneralDiagnosticOption(warningOption);
        }
    }
}
