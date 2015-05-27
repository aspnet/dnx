// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Microsoft.Framework.Runtime.Servicing;
using NuGet;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{

    public class ServicingTests
    {
        [Fact]
        public void NoPatchIsSelectedIfNothingMatches()
        {
            ServicingTest(
@"
nupkg|PatchedLib|1.0.0|lib/dnxcore50/PatchedLib.dll=patches/PatchedLib/1.0.0-patch/lib/dnxcore50/PatchedLib.dll
nupkg|NonExistingPatch2|1.0.0|lib/dnxcore50/NonExistingPatch2.dll=patches/NonExistingPatch2/1.0.0-patch/lib/dnxcore50/NonExistingPatch2.dll
nupkg|NonExisting|1.0.0|lib/dnxcore50/NonExisting.dll=patches/NonExisting/1.0.0-patch/lib/dnxcore50/NonExisting.dll
",
            (index, patchesFolder) =>
            {
                string replacementPath;

                bool foundReplacement = index.TryGetReplacement(
                    "NonExistingPatch",
                    new SemanticVersion("1.0.0"),
                    @"lib\dnxcore50\NonExistingPatch.dll",
                    out replacementPath);

                Assert.False(foundReplacement);
            });
        }

        [Fact]
        public void NoPatchIsSelectedIfVersionDoesntMatches()
        {
            ServicingTest(
@"
nupkg|PatchedLib|1.0.0|lib/dnxcore50/PatchedLib.dll=patches/PatchedLib/1.0.0-patch/lib/dnxcore50/PatchedLib.dll
nupkg|PatchedLib|1.0.2|lib/dnxcore50/PatchedLib.dll=patches/PatchedLib/1.0.2-patch/lib/dnxcore50/PatchedLib.dll
",
            (index, patchesFolder) =>
            {
                string replacementPath;

                bool foundReplacement = index.TryGetReplacement(
                    "PatchedLib",
                    // The version here doesn't match any patch
                    new SemanticVersion("1.1.0"),
                    @"lib\dnxcore50\PatchedLib.dll",
                    out replacementPath);

                Assert.False(foundReplacement);
            });
        }

        [Fact]
        public void ServicingPicksEntryIfOnlyOneMatch()
        {
            ServicingTest(
@"#should pick the last matching entry (with version)
nupkg|PatchedLib|1.0.0|lib/dnxcore50/PatchedLib.dll=patches/PatchedLib/1.0.0-patch1/lib/dnxcore50/PatchedLib.dll
",
            (index, patchesFolder) =>
            {
                string replacementPath;

                bool foundReplacement = index.TryGetReplacement(
                    "PatchedLib",
                    new SemanticVersion("1.0.0"),
                    @"lib\dnxcore50\PatchedLib.dll",
                    out replacementPath);

                Assert.True(foundReplacement);
                Assert.Equal(
                    patchesFolder + "patches/PatchedLib/1.0.0-patch1/lib/dnxcore50/PatchedLib.dll",
                    replacementPath);
            });
        }

        [Fact]
        public void ServicingPicksLatestMatchingEntryInIndex()
        {
            ServicingTest(
@"#should pick the last matching entry (with version)
nupkg|PatchedLib|1.0.0|lib/dnxcore50/PatchedLib.dll=patches/PatchedLib/1.0.0-patch1/lib/dnxcore50/PatchedLib.dll
nupkg|PatchedLib|1.0.0|lib/dnxcore50/PatchedLib.dll=patches/PatchedLib/1.0.0-patch2/lib/dnxcore50/PatchedLib.dll
nupkg|PatchedLib|1.0.1|lib/dnxcore50/PatchedLib.dll=patches/PatchedLib/1.0.1-patch1/lib/dnxcore50/PatchedLib.dll
nupkg|PatchedLib|1.0.1|lib/dnxcore50/PatchedLib.dll=patches/PatchedLib/1.0.1-patch2/lib/dnxcore50/PatchedLib.dll
",
            (index, patchesFolder) =>
            {
                string replacementPath;

                bool foundReplacement = index.TryGetReplacement(
                    "PatchedLib",
                    new SemanticVersion("1.0.0"),
                    @"lib\dnxcore50\PatchedLib.dll",
                    out replacementPath);

                Assert.True(foundReplacement);
                Assert.Equal(
                    patchesFolder + "patches/PatchedLib/1.0.0-patch2/lib/dnxcore50/PatchedLib.dll",
                    replacementPath);
            });
        }

        private static void ServicingTest(string indexContent, Action<ServicingIndex, string> validator)
        {
            const string fakePatchesFolder = @"C:/foo/patchesFolder/";

            using (MemoryStream indexStream = new MemoryStream(Encoding.UTF8.GetBytes(indexContent)))
            {
                var index = new ServicingIndex();
                index.Initialize(fakePatchesFolder, indexStream);
                validator(index, fakePatchesFolder);
            }
        }
    }
}
