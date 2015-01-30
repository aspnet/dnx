// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime
{
    internal static class Constants
    {
        public const string BootstrapperExeName = "klr";
        public const string DefaultLocalRuntimeHomeDir = ".k";
        public const string RuntimeShortName = "kre";
        public const string RuntimeNamePrefix = RuntimeShortName + "-";
        public const string WebConfigRuntimeVersion = RuntimeNamePrefix + "version";
        public const string WebConfigRuntimeFlavor = RuntimeNamePrefix + "clr";
        public const string WebConfigRuntimeAppBase = RuntimeNamePrefix + "app-base";
        public const string WebConfigKpmPackagePath = "kpm-package-path";
        public const string WebConfigBootstrapperVersion = "bootstrapper-version";
        public const string WebConfigPackagesPath = "packages-path";
        public const string BootstrapperHostName = RuntimeShortName + ".host";
        public const string BootstrapperClrName = RuntimeShortName + ".clr";
        public const string BootstrapperCoreclrManagedName = RuntimeShortName + ".coreclr.managed";
    }
}