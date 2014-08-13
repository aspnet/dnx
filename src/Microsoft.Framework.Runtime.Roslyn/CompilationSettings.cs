// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilationSettings
    {
        public LanguageVersion LanguageVersion { get; set; }
        public IEnumerable<string> Defines { get; set; }
        public CSharpCompilationOptions CompilationOptions { get; set; }

        public override bool Equals(object obj)
        {
            var settings = obj as CompilationSettings;

            return settings != null &&
                LanguageVersion.Equals(settings.LanguageVersion) &&
                Enumerable.SequenceEqual(Defines, settings.Defines) &&
                CompilationOptions.Equals(settings.CompilationOptions);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}
