// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public class ProjectFilesCollection
    {
        public static readonly string[] DefaultCompileBuiltInPatterns = new[] { @"**/*.cs" };
        public static readonly string[] DefaultBundleExcludePatterns = new[] { @"obj/**/*.*", @"bin/**/*.*", @"**/.*/**" };
        public static readonly string[] DefaultPreprocessPatterns = new[] { @"compiler/preprocess/**/*.cs" };
        public static readonly string[] DefaultSharedPatterns = new[] { @"compiler/shared/**/*.cs" };
        public static readonly string[] DefaultResourcesPatterns = new[] { @"compiler/resources/**/*" };
        public static readonly string[] DefaultContentsPatterns = new[] { @"**/*" };
        public static readonly string[] DefaultBuiltInExcludePatterns = new[] { "bin/**", "obj/**", "**/*.kproj" };

        private readonly PatternGroup _sharedPatternsGroup;
        private readonly PatternGroup _resourcePatternsGroup;
        private readonly PatternGroup _preprocessPatternsGroup;
        private readonly PatternGroup _compilePatternsGroup;
        private readonly PatternGroup _bundleExcludePatternsGroup;
        private readonly PatternGroup _contentPatternsGroup;

        private readonly string _projectDirectory;
        private readonly string _projectFilePath;

        public ProjectFilesCollection(JObject rawProject, string projectDirectory, string projectFilePath, ICollection<ICompilationMessage> warnings = null)
        {
            _projectDirectory = projectDirectory;
            _projectFilePath = projectFilePath;

            var excludeBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "excludeBuiltIn", DefaultBuiltInExcludePatterns);
            var excludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "exclude")
                                                          .Concat(excludeBuiltIns);
            var compileBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "compileBuiltIn", DefaultCompileBuiltInPatterns);

            var bundleExcludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "bundleExclude", DefaultBundleExcludePatterns);

            // The legacy names will be retired in the future.

            _sharedPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "shared", legacyName: null, warnings: warnings, fallbackIncluding: DefaultSharedPatterns, additionalExcluding: excludePatterns);

            _resourcePatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "resource", "resources", warnings: warnings, fallbackIncluding: DefaultResourcesPatterns, additionalExcluding: excludePatterns);

            _preprocessPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "preprocess", legacyName: null, warnings: warnings, fallbackIncluding: DefaultPreprocessPatterns, additionalExcluding: excludePatterns)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _compilePatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "compile", "code", warnings: warnings, additionalIncluding: compileBuiltIns, additionalExcluding: excludePatterns)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_preprocessPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _contentPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "content", "files", warnings: warnings, fallbackIncluding: DefaultContentsPatterns, additionalExcluding: excludePatterns.Concat(bundleExcludePatterns))
                .ExcludeGroup(_compilePatternsGroup)
                .ExcludeGroup(_preprocessPatternsGroup)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _bundleExcludePatternsGroup = new PatternGroup(bundleExcludePatterns);
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

        internal PatternGroup CompilePatternsGroup { get { return _compilePatternsGroup; } }

        internal PatternGroup SharedPatternsGroup { get { return _sharedPatternsGroup; } }

        internal PatternGroup ResourcePatternsGroup { get { return _resourcePatternsGroup; } }

        internal PatternGroup PreprocessPatternsGroup { get { return _preprocessPatternsGroup; } }

        internal PatternGroup BundleExcludePatternsGroup { get { return _bundleExcludePatternsGroup; } }

        internal PatternGroup ContentPatternsGroup { get { return _contentPatternsGroup; } }
    }
}