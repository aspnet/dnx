// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Specifies the severity of a <see cref="ICompilationMessage"/>.
    /// </summary>
    [AssemblyNeutral]
    public enum CompilationMessageSeverity
    {
        Info,
        Warning,
        Error,
    }
}