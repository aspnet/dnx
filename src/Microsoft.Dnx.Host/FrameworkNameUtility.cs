// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Common.Impl;

namespace Microsoft.Dnx.Host
{
    /// <summary>
    /// Parses framework names expected by the DNX _runtime_.
    /// </summary>
    /// <remarks>
    /// Note: This does not need to be complete because there are a fixed set of frameworks
    /// understood by the runtimes.
    /// </remarks>
    internal class FrameworkNameUtility
    {
        internal static bool TryParseFrameworkName(string frameworkName, out FrameworkName parsed)
        {
            parsed = null;
            
            if (frameworkName == FrameworkNames.ShortNames.Dnx451)
            {
                parsed = new FrameworkName(FrameworkNames.LongNames.Dnx, new Version(4, 5, 1));
                return true;
            }
            else if (frameworkName == FrameworkNames.ShortNames.Dnx452)
            {
                parsed = new FrameworkName(FrameworkNames.LongNames.Dnx, new Version(4, 5, 2));
                return true;
            }
            else if (frameworkName == FrameworkNames.ShortNames.Dnx46)
            {
                parsed = new FrameworkName(FrameworkNames.LongNames.Dnx, new Version(4, 6));
                return true;
            }
            else if (frameworkName == FrameworkNames.ShortNames.DnxCore50)
            {
                parsed = new FrameworkName(FrameworkNames.LongNames.DnxCore, new Version(5, 0));
                return true;
            }
            return false;
        }

        internal static FrameworkName ParseFrameworkName(string frameworkName)
        {
            FrameworkName fx;
            if (!TryParseFrameworkName(frameworkName, out fx))
            {
                throw new NotSupportedException();
            }
            return fx;
        }
    }
}
