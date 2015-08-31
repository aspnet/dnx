// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Runtime
{
    internal static class RuntimeTypes
    {
        public static readonly string CoreCLR = nameof(CoreCLR);
        public static readonly string CLR = nameof(CLR);
        public static readonly string Mono = nameof(Mono);
    }

    internal static class RuntimeArchitectures
    {
        public static readonly string X86 = "x86";
        public static readonly string X64 = "x64";
    }

    internal static class RuntimeOperatingSystems
    {
        public static readonly string Windows = nameof(Windows);
    }
}
