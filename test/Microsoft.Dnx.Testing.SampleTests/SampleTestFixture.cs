// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Testing.SampleTests
{
    public class SampleTestFixture : IDisposable
    {
        public SampleTestFixture()
        {
            Console.WriteLine($@"
Environment information:
  DNX_HOME: {Environment.GetEnvironmentVariable("DNX_HOME")}
  DNX_SDK_VERSION_FOR_TESTING: {Environment.GetEnvironmentVariable("DNX_SDK_VERSION_FOR_TESTING")}

Information of DNX under testing:
  DNX Home: {DnxSdk.GetRuntimeHome()}
  DNX Version: {DnxSdkFunctionalTestBase.SdkVersionForTesting}
");
        }

        public void Dispose()
        {
            
        }
    }
}
