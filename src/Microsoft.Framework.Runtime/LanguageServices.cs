// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime
{
    public class LanguageServices
    {
        public LanguageServices(string name, 
                                TypeInformation projectExportProvider,
                                TypeInformation compilerOptionsReader)
        {
            Name = name;
            ProjectReferenceProvider = projectExportProvider;
            CompilerOptionsReader = compilerOptionsReader;
        }

        public string Name { get; private set; }

        public TypeInformation ProjectReferenceProvider { get; private set; }

        public TypeInformation CompilerOptionsReader { get; }
    }
}