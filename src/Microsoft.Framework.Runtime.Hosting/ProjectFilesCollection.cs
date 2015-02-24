// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class ProjectFilesCollection
    {
        public static readonly string[] DefaultCompilePatterns = new[] { @"**/*.cs" };
        public static readonly string[] DefaultBundleExcludePatterns = new[] { @"obj/**/*.*", @"bin/**/*.*", @"**/.*/**" };
        public static readonly string[] DefaultPreprocessPatterns = new[] { @"compiler/preprocess/**/*.cs" };
        public static readonly string[] DefaultSharedPatterns = new[] { @"compiler/shared/**/*.cs" };
        public static readonly string[] DefaultResourcesPatterns = new[] { @"compiler/resources/**/*" };
        public static readonly string[] DefaultContentsPatterns = new[] { @"**/*" };
        public static readonly string[] DefaultBuiltInExcludePatterns = new[] { "bin/**", "obj/**", "**/*.kproj" };

        private readonly PatternsGroup _sharedPatternsGroup;
        private readonly PatternsGroup _resourcePatternsGroup;
        private readonly PatternsGroup _preprocessPatternsGroup;
        private readonly PatternsGroup _compilePatternsGroup;
        private readonly PatternsGroup _bundleExcludePatternsGroup;
        private readonly PatternsGroup _contentPatternsGroup;

        private readonly string _projectDirectory;
        private readonly string _projectFilePath;

        public ProjectFilesCollection(JObject rawProject, string projectDirectory, string projectFilePath)
        {
            _projectDirectory = projectDirectory;
            _projectFilePath = projectFilePath;

            var excludeBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "excludeBuiltIn", DefaultBuiltInExcludePatterns);
            var excludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "exclude")
                                                          .Concat(excludeBuiltIns);

            var bundleExcludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "bundleExclude", DefaultBundleExcludePatterns);

            _sharedPatternsGroup = PatternsGroup.Build(rawProject, projectDirectory, projectFilePath, "shared", legacyName: null, fallback: DefaultSharedPatterns, buildInExcludePatterns: excludePatterns);

            _resourcePatternsGroup = PatternsGroup.Build(rawProject, projectDirectory, projectFilePath, "resource", "resources", DefaultResourcesPatterns, excludePatterns);

            _preprocessPatternsGroup = PatternsGroup.Build(rawProject, projectDirectory, projectFilePath, "preprocess", legacyName: null, fallback: DefaultPreprocessPatterns, buildInExcludePatterns: excludePatterns)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _compilePatternsGroup = PatternsGroup.Build(rawProject, projectDirectory, projectFilePath, "compile", "code", DefaultCompilePatterns, excludePatterns)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_preprocessPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _contentPatternsGroup = PatternsGroup.Build(rawProject, projectDirectory, projectFilePath, "content", "files", DefaultContentsPatterns, excludePatterns.Concat(bundleExcludePatterns).Distinct())
                .ExcludeGroup(_compilePatternsGroup)
                .ExcludeGroup(_preprocessPatternsGroup)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _bundleExcludePatternsGroup = new PatternsGroup(bundleExcludePatterns);
        }

        public IEnumerable<string> SourceFiles
        {
            get { return _compilePatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> PreprocessSourceFiles
        {
            get { return _preprocessPatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> BundleExcludeFiles
        {
            get { return _bundleExcludePatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> ResourceFiles
        {
            get { return _resourcePatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> SharedFiles
        {
            get { return _sharedPatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> ContentFiles
        {
            get { return _contentPatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public PatternsGroup CompilePatternsGroup { get { return _compilePatternsGroup; } }

        public PatternsGroup SharedPatternsGroup { get { return _sharedPatternsGroup; } }

        public PatternsGroup ResourcePatternsGroup { get { return _resourcePatternsGroup; } }

        public PatternsGroup PreprocessPatternsGroup { get { return _preprocessPatternsGroup; } }

        public PatternsGroup BundleExcludePatternsGroup { get { return _bundleExcludePatternsGroup; } }

        public PatternsGroup ContentPatternsGroup { get { return _contentPatternsGroup; } }
    }
}