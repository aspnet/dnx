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
        public static readonly string[] DefaultPublishExcludePatterns = new[] { @"obj/**/*.*", @"bin/**/*.*", @"**/.*/**" };
        public static readonly string[] DefaultPreprocessPatterns = new[] { @"compiler/preprocess/**/*.cs" };
        public static readonly string[] DefaultSharedPatterns = new[] { @"compiler/shared/**/*.cs" };
        public static readonly string[] DefaultResourcesPatterns = new[] { @"compiler/resources/**/*" };
        public static readonly string[] DefaultContentsPatterns = new[] { @"**/*" };
        public static readonly string[] DefaultBuiltInExcludePatterns = new[] { "bin/**", "obj/**", "**/*.kproj" };

        private readonly PatternGroup _sharedPatternsGroup;
        private readonly PatternGroup _resourcePatternsGroup;
        private readonly PatternGroup _preprocessPatternsGroup;
        private readonly PatternGroup _compilePatternsGroup;
        private readonly PatternGroup _contentPatternsGroup;

        private readonly string _projectDirectory;
        private readonly string _projectFilePath;

        private readonly IEnumerable<string> _publishExcludePatterns;

        public ProjectFilesCollection(JObject rawProject, string projectDirectory, string projectFilePath, ICollection<ICompilationMessage> warnings = null)
        {
            _projectDirectory = projectDirectory;
            _projectFilePath = projectFilePath;

            var excludeBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "excludeBuiltIn", DefaultBuiltInExcludePatterns);
            var excludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "exclude")
                                                          .Concat(excludeBuiltIns);
            var compileBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "compileBuiltIn", DefaultCompileBuiltInPatterns);

            // TODO: The legacy names will be retired in the future.
            var legacyPublishExcludePatternName = "bundleExclude";
            var legacyPublishExcludePatternToken = rawProject[legacyPublishExcludePatternName];
            if (legacyPublishExcludePatternToken != null)
            {
                _publishExcludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, legacyPublishExcludePatternName, DefaultPublishExcludePatterns);
                if (warnings != null)
                {
                    warnings.Add(new FileFormatMessage(
                        string.Format("Property \"{0}\" is deprecated. It is replaced by \"{1}\".", legacyPublishExcludePatternName, "publishExclude"),
                        projectFilePath,
                        CompilationMessageSeverity.Warning,
                        legacyPublishExcludePatternToken));
                }
            }
            else
            {
                _publishExcludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "publishExclude", DefaultPublishExcludePatterns);
            }

            _sharedPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "shared", legacyName: null, warnings: warnings, fallbackIncluding: DefaultSharedPatterns, additionalExcluding: excludePatterns);

            _resourcePatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "resource", "resources", warnings: warnings, fallbackIncluding: DefaultResourcesPatterns, additionalExcluding: excludePatterns);

            _preprocessPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "preprocess", legacyName: null, warnings: warnings, fallbackIncluding: DefaultPreprocessPatterns, additionalExcluding: excludePatterns)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _compilePatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "compile", "code", warnings: warnings, additionalIncluding: compileBuiltIns, additionalExcluding: excludePatterns)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_preprocessPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _contentPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "content", "files", warnings: warnings, fallbackIncluding: DefaultContentsPatterns, additionalExcluding: excludePatterns.Concat(_publishExcludePatterns))
                .ExcludeGroup(_compilePatternsGroup)
                .ExcludeGroup(_preprocessPatternsGroup)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);
        }

        public IEnumerable<string> SourceFiles
        {
            get { return _compilePatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> PreprocessSourceFiles
        {
            get { return _preprocessPatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> ResourceFiles
        {
            get { return _resourcePatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> SharedFiles
        {
            get { return _sharedPatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> GetFilesForBundling(bool includeSource, IEnumerable<string> additionalExcludePatterns)
        {
            var patternGroup = new PatternGroup(ContentPatternsGroup.IncludePatterns,
                                                ContentPatternsGroup.ExcludePatterns.Concat(additionalExcludePatterns),
                                                ContentPatternsGroup.IncludeLiterals);
            if (!includeSource)
            {
                foreach (var excludedGroup in ContentPatternsGroup.ExcludePatternsGroup)
                {
                    patternGroup.ExcludeGroup(excludedGroup);
                }
            }

            return patternGroup.SearchFiles(_projectDirectory);
        }

        internal PatternGroup CompilePatternsGroup { get { return _compilePatternsGroup; } }

        internal PatternGroup SharedPatternsGroup { get { return _sharedPatternsGroup; } }

        internal PatternGroup ResourcePatternsGroup { get { return _resourcePatternsGroup; } }

        internal PatternGroup PreprocessPatternsGroup { get { return _preprocessPatternsGroup; } }

        internal PatternGroup ContentPatternsGroup { get { return _contentPatternsGroup; } }
    }
}
