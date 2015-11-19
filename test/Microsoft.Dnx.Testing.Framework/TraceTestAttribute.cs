// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Xunit.Sdk;

namespace Microsoft.Dnx.Testing.Framework
{
    public class TraceTestAttribute : BeforeAfterTestAttribute
    {
        public override void Before(MethodInfo methodUnderTest)
        {
            TestLogger.TraceInformation($"Begin test: {methodUnderTest.DeclaringType.FullName}{methodUnderTest.Name} {Environment.NewLine}");
        }

        public override void After(MethodInfo methodUnderTest)
        {
            TestLogger.TraceInformation($"Completed test: {methodUnderTest.DeclaringType.FullName}.{methodUnderTest.Name} {Environment.NewLine}");
        }
    }
}
