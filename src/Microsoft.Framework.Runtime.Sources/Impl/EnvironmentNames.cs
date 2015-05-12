// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime
{
    internal static class EnvironmentNames
    {
        public static readonly string CommonPrefix = Constants.RuntimeShortName.ToUpper() + "_";
        public static readonly string Packages = "NUGET_PACKAGES";
        public static readonly string DnxPackages = CommonPrefix + "PACKAGES";
        public static readonly string PackagesCache = CommonPrefix + "PACKAGES_CACHE";
        public static readonly string Servicing = CommonPrefix + "SERVICING";
        public static readonly string Trace = CommonPrefix + "TRACE";
        public static readonly string CompilationServerPort = CommonPrefix + "COMPILATION_SERVER_PORT";
        public static readonly string Home = CommonPrefix + "HOME";
        public static readonly string GlobalPath = CommonPrefix + "GLOBAL_PATH";
        public static readonly string AppBase = CommonPrefix + "APPBASE";
        public static readonly string Framework = CommonPrefix + "FRAMEWORK";
        public static readonly string Configuration = CommonPrefix + "CONFIGURATION";
        public static readonly string ConsoleHost = CommonPrefix + "CONSOLE_HOST";
        public static readonly string DefaultLib = CommonPrefix + "DEFAULT_LIB";
        public static readonly string BuildKeyFile = CommonPrefix + "BUILD_KEY_FILE";
        public static readonly string BuildDelaySign = CommonPrefix + "BUILD_DELAY_SIGN";
    }
}