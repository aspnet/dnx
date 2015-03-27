// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime
{
    public class CompilationMessage : ICompilationMessage
    {
        public string SourceFilePath { get; set; }

        public string Message { get; set; }

        public int EndColumn { get; set; }

        public int EndLine { get; set; }

        public int StartColumn { get; set; }

        public int StartLine { get; set; }

        public string FormattedMessage { get; set; }

        public CompilationMessageSeverity Severity { get; set; }
    }
}