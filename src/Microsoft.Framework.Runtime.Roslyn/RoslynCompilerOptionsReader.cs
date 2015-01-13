// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynCompilerOptionsReader : ICompilerOptionsReader
    {
        public ICompilerOptions ReadCompilerOptions(string json)
        {
            return GetCompilationOptions(json);
        }

        public ICompilerOptions ReadConfigurationCompilerOptions(string json, string configuration)
        {
            var options = GetCompilationOptions(json);
            // Set defaults if the project.json does not specify values for Optimize and Defines
            if (string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase))
            {
                options.Optimize = options.Optimize ?? false;
                if (options.Defines == null)
                {
                    options.Defines = new[] { "DEBUG", "TRACE" };
                }
            }
            else if (string.Equals(configuration, "Release", StringComparison.OrdinalIgnoreCase))
            {
                options.Optimize = options.Optimize ?? true;
                if (options.Defines == null)
                {
                    options.Defines = new[] { "RELEASE", "TRACE" };
                }
            }

            return options;
        }

        public ICompilerOptions ReadFrameworkCompilerOptions(string json, string shortName, FrameworkName targetFramework)
        {
            var options = GetCompilationOptions(json);
            var frameworkDefine = MakeDefaultTargetFrameworkDefine(shortName, targetFramework);

            if (!string.IsNullOrEmpty(frameworkDefine))
            {
                var defines = new HashSet<string>(options.Defines ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
                defines.Add(frameworkDefine);
                options.Defines = defines;
            }

            return options;
        }

        private static RoslynCompilerOptions GetCompilationOptions(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
            {
                return new RoslynCompilerOptions();
            }

            var rawOptions = JToken.Parse(jsonContent);

            return new RoslynCompilerOptions
            {
                LanguageVersion = ConvertValue<string>(rawOptions, "languageVersion"),
                AllowUnsafe = GetValue<bool?>(rawOptions, "allowUnsafe"),
                Platform = GetValue<string>(rawOptions, "platform"),
                WarningsAsErrors = GetValue<bool?>(rawOptions, "warningsAsErrors"),
                Optimize = GetValue<bool?>(rawOptions, "optimize"),
                Defines = ConvertValue<string[]>(rawOptions, "define")
            };
        }

        private static string MakeDefaultTargetFrameworkDefine(string shortName, FrameworkName targetFramework)
        {
            if (VersionUtility.IsPortableFramework(targetFramework))
            {
                return null;
            }

            return shortName.ToUpperInvariant();
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
    }
}