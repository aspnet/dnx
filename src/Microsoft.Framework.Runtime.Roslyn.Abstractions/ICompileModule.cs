// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// Module that allows plugging into the compilation pipeline
    /// </summary>
    public interface ICompileModule
    {
        /// <summary>
        /// Runs after the roslyn compilation is created but before anything is emitted
        /// </summary>
        /// <param name="context"></param>
        void BeforeCompile(BeforeCompileContext context);

        /// <summary>
        /// Runs after the compilation is emitted. Changing the compilation will not have any effect at this point
        /// but the assembly can be changed before it is saved on disk or loaded into memory.
        /// </summary>
        /// <param name="context"></param>
        void AfterCompile(AfterCompileContext context);
    }
}
