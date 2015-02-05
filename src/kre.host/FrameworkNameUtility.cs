// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;

namespace kre.host
{
    internal class FrameworkNameUtility
    {
        internal static FrameworkName ParseFrameworkName(string frameworkName)
        {
            if (frameworkName == "aspnet50")
            {
                return new FrameworkName("Asp.Net", new Version(5, 0));
            }
            else if (frameworkName == "aspnetcore50")
            {
                return new FrameworkName("Asp.NetCore", new Version(5, 0));
            }

            throw new NotSupportedException();
        }
    }
}
