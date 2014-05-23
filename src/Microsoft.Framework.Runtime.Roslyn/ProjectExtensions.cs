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
        public static CompilationSettings GetCompilationSettings(this Project project, FrameworkName targetFramework)
        {
            // TODO: Don't parse stuff everytime

            var rootOptions = project.GetCompilationOptions();
            var rootDefines = ConvertValue<string[]>(rootOptions, "define") ?? new string[] { };
            var languageVersionValue = ConvertValue<string>(rootOptions, "languageVersion");

            var configuration = project.GetConfiguration(targetFramework);

            JToken specificOptions = null;
            string[] specificDefines = null;

            if (configuration.Value == null)
            {
                specificDefines = new string[] { };
            }
            else
            {
                specificOptions = configuration.Value["compilationOptions"];
                specificDefines = ConvertValue<string[]>(specificOptions, "define") ??
                    new[] {
                        MakeDefaultTargetFrameworkDefine(targetFramework)
                    };
            }

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

            var assemblyIdentityComparer = VersionUtility.IsDesktop(targetFramework) ?
                DesktopAssemblyIdentityComparer.Default : null;

            options = options.WithAssemblyIdentityComparer(assemblyIdentityComparer);

            LanguageVersion languageVersion;
            if (!Enum.TryParse<LanguageVersion>(value: languageVersionValue,
                                                ignoreCase: true,
                                                result: out languageVersion))
            {
                // REVIEW: Should the default be C# 6 or experimental?
                languageVersion = LanguageVersion.CSharp6;
            }

            var settings = new CompilationSettings
            {
                LanguageVersion = languageVersion,
                Defines = rootDefines.Concat(specificDefines),
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

        private static CSharpCompilationOptions GetCompilationOptions(JToken compilationOptions)
        {
            if (compilationOptions == null)
            {
                return null;
            }

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                                .WithHighEntropyVirtualAddressSpace(true);

            bool allowUnsafe = GetValue<bool>(compilationOptions, "allowUnsafe");
            string platformValue = GetValue<string>(compilationOptions, "platform");
            bool warningsAsErrors = GetValue<bool>(compilationOptions, "warningsAsErrors");

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

        private static T ConvertValue<T>(JToken token, string name)
        {
            if (token == null)
            {
                return default(T);
            }

            var obj = token[name];

            if (obj == null)
            {
                return default(T);
            }

            return obj.ToObject<T>();
        }

        private static T GetValue<T>(JToken token, string name)
        {
            if (token == null)
            {
                return default(T);
            }

            var obj = token[name];

            if (obj == null)
            {
                return default(T);
            }

            return obj.Value<T>();
        }
    }
}
