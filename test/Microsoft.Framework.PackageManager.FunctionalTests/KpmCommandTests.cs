// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.FunctionalTestUtils;
using Xunit;

namespace Microsoft.Framework.PackageManager
{
    public class KpmCommandTests
    {
        public static IEnumerable<object[]> KrePaths
        {
            get
            {
                foreach (var path in TestUtils.GetUnpackedKrePaths())
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmCommandReturnsNonZeroExitCodeWhenNoArgumentWasGiven(DisposableDirPath krePath)
        {
            using (krePath)
            {
                var exitCode = KpmTestUtils.ExecKpm(
                    krePath,
                    subcommand: string.Empty,
                    arguments: string.Empty);

                Assert.NotEqual(0, exitCode);
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KCommandReturnsZeroExitCodeWhenHelpOptionWasGiven(DisposableDirPath krePath)
        {
            using (krePath)
            {
                var exitCode = KpmTestUtils.ExecKpm(
                    krePath,
                    subcommand: string.Empty,
                    arguments: "--help");

                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmSlashQuestionMarkDoesNotShowHelpInformationOfCmd(DisposableDirPath krePath)
        {
            using (krePath)
            {
                var exitCode = KpmTestUtils.ExecKpm(
                    krePath,
                    subcommand: string.Empty,
                    arguments: "/?");

                // If "kpm /?" shows help information of cmd.exe, the exit code is 0
                Assert.NotEqual(0, exitCode);
            }
        }
    }
}
