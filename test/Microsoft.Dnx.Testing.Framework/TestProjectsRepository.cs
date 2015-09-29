// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Dnx.Testing.Framework
{
    public static class TestProjectsRepository
    {
        private static readonly string _root;
        private static readonly Dictionary<string, Lazy<Solution>> _solutions;

        static TestProjectsRepository()
        {
            _root = Path.Combine(TestUtils.RootTestFolder, "SharedSolutions");

            _solutions = Directory.GetDirectories(TestUtils.GetTestSolutionsDirectory())
                                  .Select(CreateLazySolution)
                                  .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        /// <summary>
        /// Find a readonly copy of the test project according to the given name.
        ///
        /// The test project is copied and restored for single test run. The content
        /// of the test project must not be modified otherwise other tests will be
        /// impacted.
        /// </summary>
        public static Solution EnsureRestoredSolution(string solutionName)
        {
            Lazy<Solution> solution;
            if (_solutions.TryGetValue(solutionName, out solution))
            {
                return solution.Value;
            }

            return null;
        }

        private static KeyValuePair<string, Lazy<Solution>> CreateLazySolution(string sourceDirectory)
        {
            var solutionName = Path.GetFileName(sourceDirectory);

            var lasySolution = new Lazy<Solution>(() =>
            {
                var target = Path.Combine(_root, solutionName);
                TestUtils.CopyFolder(sourceDirectory, target);

                var solution = new Solution(target);

                var sdk = (DnxSdk)DnxSdkFunctionalTestBase.ClrDnxSdks.First()[0];
                sdk.Dnu.Restore(solution);

                return solution;
            }, isThreadSafe: true);

            return new KeyValuePair<string, Lazy<Solution>>(solutionName, lasySolution);
        }
    }
}
