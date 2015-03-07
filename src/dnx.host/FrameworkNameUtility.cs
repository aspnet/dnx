// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Common.Impl;

namespace dnx.host
{
    internal class FrameworkNameUtility
    {
        internal static FrameworkName ParseFrameworkName(string frameworkName)
        {
            if (frameworkName == FrameworkNames.ShortNames.Dnx451)
            {
                return new FrameworkName(FrameworkNames.LongNames.Dnx, new Version(4, 5, 1));
            }
            else if (frameworkName == FrameworkNames.ShortNames.Dnx46)
            {
                return new FrameworkName(FrameworkNames.LongNames.Dnx, new Version(4, 6));
            }
            else if (frameworkName == FrameworkNames.ShortNames.DnxCore50)
            {
                return new FrameworkName(FrameworkNames.LongNames.DnxCore, new Version(5, 0));
            }

            throw new NotSupportedException();
        }
    }
}
