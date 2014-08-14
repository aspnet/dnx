// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Runtime.Common
{
    internal static class ConfigurationHelper
    {
        public static string NormalizeConfigurationName(string name)
        {
            if (string.Equals(name, "Release", StringComparison.OrdinalIgnoreCase))
            {
                return "Release";
            }
            else if (string.Equals(name, "Debug", StringComparison.OrdinalIgnoreCase))
            {
                return "Debug";
            }

            throw new ArgumentException("TODO: Configuration name must be 'Release' or 'Debug'");
        }
    }
}