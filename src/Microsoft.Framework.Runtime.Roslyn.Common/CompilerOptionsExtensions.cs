// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public static class CompilerOptionsExtensions
    {
        private const string NetFrameworkIdentifier = ".NETFramework";
        private const string AspNetFrameworkIdentifier = "Asp.Net";

        public static CompilationSettings ToCompilationSettings(this ICompilerOptions compilerOptions,
                                                                FrameworkName targetFramework)
        {
            var options = GetCompilationOptions(compilerOptions);

            // Disable 1702 until roslyn turns this off by default
            options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                { "CS1702", ReportDiagnostic.Suppress },
                { "CS1705", ReportDiagnostic.Suppress }
            });

            if (PlatformHelper.IsMono)
            {
                options = options.WithConcurrentBuild(concurrentBuild: false);
            }

            AssemblyIdentityComparer assemblyIdentityComparer =
#if ASPNET50
                IsDesktop(targetFramework) ?
                DesktopAssemblyIdentityComparer.Default :
#endif
                null;

            options = options.WithAssemblyIdentityComparer(assemblyIdentityComparer);

            LanguageVersion languageVersion;
            if (!Enum.TryParse<LanguageVersion>(value: compilerOptions.LanguageVersion,
                                                ignoreCase: true,
                                                result: out languageVersion))
            {
                languageVersion = LanguageVersion.CSharp6;
            }

            var settings = new CompilationSettings
            {
                LanguageVersion = languageVersion,
                Defines = compilerOptions.Defines ?? Enumerable.Empty<string>(),
                CompilationOptions = options
            };

            return settings;
        }

        private static CSharpCompilationOptions GetCompilationOptions(ICompilerOptions compilerOptions)
        {
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            string platformValue = compilerOptions.Platform;
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

            ReportDiagnostic warningOption = warningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default;

            return options.WithAllowUnsafe(allowUnsafe)
                          .WithPlatform(platform)
                          .WithGeneralDiagnosticOption(warningOption)
                          .WithOptimizationLevel(optimize ? OptimizationLevel.Release : OptimizationLevel.Debug);
        }

        private static bool IsDesktop(FrameworkName frameworkName)
        {
            return frameworkName.Identifier == NetFrameworkIdentifier ||
                   frameworkName.Identifier == AspNetFrameworkIdentifier;
        }
    }
}
