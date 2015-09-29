// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Testing.Framework
{
    public class DnxSdkFunctionalTestFixtureBase
    {
        public DnxSdkFunctionalTestFixtureBase()
        {
            Console.WriteLine($@"
Environment information:
  DNX_HOME: {Environment.GetEnvironmentVariable(EnvironmentNames.Home)}
  DNX_TEST_SDK_VERSION: {Environment.GetEnvironmentVariable(TestEnvironmentNames.TestSdkVersion)}

Information of DNX under testing:
  DNX Home: {DnxSdk.GetRuntimeHome()}
  DNX Version: {DnxSdkFunctionalTestBase.SdkVersionForTesting}
");
        }

        public void Dispose()
        {
            var saveFilesBehaviour = Environment.GetEnvironmentVariable(TestEnvironmentNames.SaveFiles) ?? string.Empty;
            if (saveFilesBehaviour.Equals(TestConstants.SaveFilesNone, StringComparison.OrdinalIgnoreCase))
            {
                var testFolder = TestUtils.RootTestFolder;
                if (Directory.Exists(testFolder))
                {
                    Directory.Delete(testFolder, recursive: true);
                }
            }
        }
    }
}
