// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Dnx.Runtime.Common.Impl;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public static class CompilerOptionsExtensions
    {
        private const string NetFrameworkIdentifier = ".NETFramework";

        public static CompilationSettings ToCompilationSettings(this ICompilerOptions compilerOptions,
                                                                FrameworkName targetFramework,
                                                                string projectDirectory)
        {
            var options = GetCompilationOptions(compilerOptions, projectDirectory);

            // Disable 1702 until roslyn turns this off by default
            options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                { "CS1701", ReportDiagnostic.Suppress }, // Binding redirects
                { "CS1702", ReportDiagnostic.Suppress },
                { "CS1705", ReportDiagnostic.Suppress }
            });

            if (RuntimeEnvironmentHelper.IsMono)
            {
                options = options.WithConcurrentBuild(concurrentBuild: false);
            }

            AssemblyIdentityComparer assemblyIdentityComparer =
#if NET451
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

        private static CSharpCompilationOptions GetCompilationOptions(ICompilerOptions compilerOptions, string projectDirectory)
        {
            var outputKind = compilerOptions.EmitEntryPoint.GetValueOrDefault() ?
                OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary;
            var options = new CSharpCompilationOptions(outputKind);

            string platformValue = compilerOptions.Platform;
            bool allowUnsafe = compilerOptions.AllowUnsafe ?? false;
            bool optimize = compilerOptions.Optimize ?? false;
            bool warningsAsErrors = compilerOptions.WarningsAsErrors ?? false;

            Platform platform;
            if (!Enum.TryParse(value: platformValue, ignoreCase: true, result: out platform))
            {
                platform = Platform.AnyCpu;
            }

            options = options
                        .WithAllowUnsafe(allowUnsafe)
                        .WithPlatform(platform)
                        .WithGeneralDiagnosticOption(warningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default)
                        .WithOptimizationLevel(optimize ? OptimizationLevel.Release : OptimizationLevel.Debug);

            return AddSigningOptions(options, compilerOptions, projectDirectory);
        }

        private static CSharpCompilationOptions AddSigningOptions(CSharpCompilationOptions options, ICompilerOptions compilerOptions, string projectDirectory)
        {
            var useOssSigning = compilerOptions.UseOssSigning == true;

            var keyFile =
                Environment.GetEnvironmentVariable(EnvironmentNames.BuildKeyFile) ??
                GetKeyFileFullPath(projectDirectory, compilerOptions.KeyFile);

            if (!string.IsNullOrWhiteSpace(keyFile))
            {
#if DOTNET5_4
                return options.WithCryptoPublicKey(
                    SnkUtils.ExtractPublicKey(File.ReadAllBytes(keyFile)));
#else
                if (RuntimeEnvironmentHelper.IsMono || useOssSigning)
                {
                    return options.WithCryptoPublicKey(
                        SnkUtils.ExtractPublicKey(File.ReadAllBytes(keyFile)));
                }

                options = options.WithCryptoKeyFile(keyFile);

                var delaySignString = Environment.GetEnvironmentVariable(EnvironmentNames.BuildDelaySign);
                var delaySign =
                    delaySignString == null
                        ? compilerOptions.DelaySign
                        : string.Equals(delaySignString, "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(delaySignString, "1", StringComparison.Ordinal);

                return options.WithDelaySign(delaySign);
#endif
            }

            return useOssSigning ? options.WithCryptoPublicKey(StrongNameKey) : options;
        }

        private static string GetKeyFileFullPath(string projectDirectory, string keyFile)
        {
            return string.IsNullOrWhiteSpace(keyFile) ? keyFile : Path.GetFullPath(Path.Combine(projectDirectory, keyFile));
        }

        private static bool IsDesktop(FrameworkName frameworkName)
        {
            return frameworkName.Identifier == NetFrameworkIdentifier ||
                   frameworkName.Identifier == FrameworkNames.LongNames.Dnx;
        }

        private static readonly ImmutableArray<byte> StrongNameKey =
            new byte[]
            {
                0x00, 0x24, 0x00, 0x00, 0x04, 0x80, 0x00, 0x00, 0x94, 0x00, 0x00, 0x00, 0x06, 0x02, 0x00, 0x00,
                0x00, 0x24, 0x00, 0x00, 0x52, 0x53, 0x41, 0x31, 0x00, 0x04, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
                0xb1, 0xbe, 0x0d, 0x2b, 0xc7, 0xc1, 0x50, 0xf3, 0xe2, 0x67, 0x10, 0x3e, 0x15, 0x09, 0x9f, 0x35,
                0xb0, 0x16, 0x3d, 0xb1, 0x82, 0x13, 0xc5, 0x01, 0x43, 0xb7, 0x48, 0xda, 0x46, 0xf6, 0x53, 0x0e,
                0x42, 0x50, 0x6e, 0x09, 0x50, 0x33, 0x0c, 0xf4, 0xac, 0xc3, 0xef, 0x24, 0x30, 0x69, 0xf9, 0x74,
                0x23, 0x89, 0x3b, 0x4c, 0x3f, 0x24, 0x85, 0x51, 0xbe, 0x15, 0x50, 0x9c, 0xf6, 0x98, 0x4a, 0xab,
                0xfa, 0x1d, 0xc6, 0x9c, 0xa2, 0x55, 0xc2, 0x15, 0x49, 0x3c, 0xcc, 0x88, 0x16, 0xb3, 0x04, 0x44,
                0xaf, 0x20, 0xbe, 0x56, 0x78, 0x81, 0xcc, 0xd5, 0x3c, 0x3b, 0xce, 0x52, 0x00, 0xbf, 0x76, 0x81,
                0x30, 0xbc, 0xba, 0x41, 0x81, 0x0e, 0x81, 0xb7, 0x79, 0xce, 0xea, 0x51, 0x83, 0xf7, 0x5c, 0x16,
                0x56, 0xf9, 0xcb, 0x3c, 0x4f, 0x2a, 0x7a, 0x60, 0x66, 0xbb, 0x74, 0xbd, 0x5a, 0xfe, 0xb2, 0xcd
            }.ToImmutableArray();
    }
}
