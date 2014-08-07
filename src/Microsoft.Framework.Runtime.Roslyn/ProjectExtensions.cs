// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public static class ProjectExtensions
    {
        public static CompilationSettings GetCompilationSettings(this Project project, FrameworkName targetFramework, string configuration)
        {
            // Get all project options and combine them
            var rootOptions = project.GetCompilerOptions();
            var configurationOptions = project.GetCompilerOptions(configuration);
            var targetFrameworkOptions = project.GetCompilerOptions(targetFramework);

            // Combine all of the options
            var resultOptions = CompilerOptions.Combine(rootOptions, configurationOptions, targetFrameworkOptions);

            var options = GetCompilationOptions(resultOptions);

            // Disable 1702 until roslyn turns this off by default
            options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                { "CS1702", ReportDiagnostic.Suppress }
            });

            if (PlatformHelper.IsMono)
            {
                options = options.WithConcurrentBuild(concurrentBuild: false);
            }

            AssemblyIdentityComparer assemblyIdentityComparer =
#if NET45
                VersionUtility.IsDesktop(targetFramework) ?
                DesktopAssemblyIdentityComparer.Default : 
#endif
                null;

            options = options.WithAssemblyIdentityComparer(assemblyIdentityComparer);

            LanguageVersion languageVersion;
            if (!Enum.TryParse<LanguageVersion>(value: resultOptions.LanguageVersion,
                                                ignoreCase: true,
                                                result: out languageVersion))
            {
                languageVersion = LanguageVersion.Experimental;
            }

            var settings = new CompilationSettings
            {
                LanguageVersion = languageVersion,
                Defines = resultOptions.Defines,
                CompilationOptions = options
            };

            return settings;
        }

        private static CSharpCompilationOptions GetCompilationOptions(CompilerOptions compilerOptions)
        {
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            string platformValue = compilerOptions.Platform;
            string debugSymbolsValue = compilerOptions.DebugSymbols;
            bool allowUnsafe = compilerOptions.AllowUnsafe ?? false;
            bool optimize = compilerOptions.Optimize ?? false;
            bool warningsAsErrors = compilerOptions.WarningsAsErrors ?? false;

            Platform platform;
            if (!Enum.TryParse<Platform>(value: platformValue,
                                         ignoreCase: true,
                                         result: out platform))
            {
                platform = Platform.AnyCpu;
            }

            DebugInformationKind debugInformationKind;
            if (!Enum.TryParse<DebugInformationKind>(debugSymbolsValue,
                                                     ignoreCase: true,
                                                     result: out debugInformationKind))
            {
                debugInformationKind = DebugInformationKind.Full;
            }

            ReportDiagnostic warningOption = warningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default;

            return options.WithAllowUnsafe(allowUnsafe)
                          .WithPlatform(platform)
                          .WithGeneralDiagnosticOption(warningOption)
                          .WithOptimizations(optimize)
                          .WithDebugInformationKind(debugInformationKind);
        }
    }
}
