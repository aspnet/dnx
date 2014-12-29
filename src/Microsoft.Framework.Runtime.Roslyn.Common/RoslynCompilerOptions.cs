// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.Runtime
{
    public class RoslynCompilerOptions : ICompilerOptions
    {
        public IEnumerable<string> Defines { get; set; }

        public string LanguageVersion { get; set; }

        public string Platform { get; set; }

        public bool? AllowUnsafe { get; set; }

        public bool? WarningsAsErrors { get; set; }

        public bool? Optimize { get; set; }

        public ICompilerOptions Merge(ICompilerOptions options)
        {
            var other = (RoslynCompilerOptions)options;
            var result = new RoslynCompilerOptions();
            foreach (var option in new[] { this, other })
            {
                // Skip null options
                if (option == null)
                {
                    continue;
                }

                if (option.Defines != null)
                {
                    // Defines are always combined
                    result.Defines = (result.Defines ?? Enumerable.Empty<string>()).Concat(option.Defines)
                                                                                   .Distinct(StringComparer.Ordinal);
                }

                if (option.LanguageVersion != null)
                {
                    result.LanguageVersion = option.LanguageVersion;
                }

                if (option.Platform != null)
                {
                    result.Platform = option.Platform;
                }

                if (option.AllowUnsafe != null)
                {
                    result.AllowUnsafe = option.AllowUnsafe;
                }

                if (option.WarningsAsErrors != null)
                {
                    result.WarningsAsErrors = option.WarningsAsErrors;
                }

                if (option.Optimize != null)
                {
                    result.Optimize = option.Optimize;
                }
            }

            return result;
        }
    }
}