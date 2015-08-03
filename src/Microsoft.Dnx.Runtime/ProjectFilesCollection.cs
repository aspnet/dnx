// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Runtime.Json;

namespace Microsoft.Dnx.Runtime
{
    public class ProjectFilesCollection
    {
        public static readonly string[] DefaultCompileBuiltInPatterns = new[] { @"**/*.cs" };
        public static readonly string[] DefaultPublishExcludePatterns = new[] { @"obj/**/*.*", @"bin/**/*.*", @"**/.*/**" };
        public static readonly string[] DefaultPreprocessPatterns = new[] { @"compiler/preprocess/**/*.cs" };
        public static readonly string[] DefaultSharedPatterns = new[] { @"compiler/shared/**/*.cs" };
        public static readonly string[] DefaultResourcesBuiltInPatterns = new[] { @"compiler/resources/**/*", "**/*.resx" };
        public static readonly string[] DefaultContentsBuiltInPatterns = new[] { @"**/*" };

        public static readonly string[] DefaultBuiltInExcludePatterns = new[] { "bin/**", "obj/**", "**/*.xproj" };

        private readonly PatternGroup _sharedPatternsGroup;
        private readonly PatternGroup _resourcePatternsGroup;
        private readonly PatternGroup _preprocessPatternsGroup;
        private readonly PatternGroup _compilePatternsGroup;
        private readonly PatternGroup _contentPatternsGroup;
        private readonly IDictionary<string, string> _namedResources;

        private readonly string _projectDirectory;
        private readonly string _projectFilePath;

        private readonly IEnumerable<string> _publishExcludePatterns;

        internal ProjectFilesCollection(JsonObject rawProject,
                                        string projectDirectory,
                                        string projectFilePath,
                                        ICollection<DiagnosticMessage> warnings = null)
        {
            _projectDirectory = projectDirectory;
            _projectFilePath = projectFilePath;

            var excludeBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "excludeBuiltIn", DefaultBuiltInExcludePatterns);
            var excludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "exclude")
                                                          .Concat(excludeBuiltIns);
            var contentBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "contentBuiltIn", DefaultContentsBuiltInPatterns);
            var compileBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "compileBuiltIn", DefaultCompileBuiltInPatterns);
            var resourceBuiltIns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "resourceBuiltIn", DefaultResourcesBuiltInPatterns);

            // TODO: The legacy names will be retired in the future.
            var legacyPublishExcludePatternName = "bundleExclude";
            var legacyPublishExcludePatternToken = rawProject.ValueAsJsonObject(legacyPublishExcludePatternName);
            if (legacyPublishExcludePatternToken != null)
            {
                _publishExcludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, legacyPublishExcludePatternName, DefaultPublishExcludePatterns);
                if (warnings != null)
                {
                    warnings.Add(new DiagnosticMessage(
                        string.Format("Property \"{0}\" is deprecated. It is replaced by \"{1}\".", legacyPublishExcludePatternName, "publishExclude"),
                        projectFilePath,
                        DiagnosticMessageSeverity.Warning,
                        legacyPublishExcludePatternToken.Line,
                        legacyPublishExcludePatternToken.Column));
                }
            }
            else
            {
                _publishExcludePatterns = PatternsCollectionHelper.GetPatternsCollection(rawProject, projectDirectory, projectFilePath, "publishExclude", DefaultPublishExcludePatterns);
            }

            _sharedPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "shared", legacyName: null, warnings: warnings, fallbackIncluding: DefaultSharedPatterns, additionalExcluding: excludePatterns);

            _resourcePatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "resource", "resources", warnings: warnings, additionalIncluding: resourceBuiltIns, additionalExcluding: excludePatterns);

            _preprocessPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "preprocess", legacyName: null, warnings: warnings, fallbackIncluding: DefaultPreprocessPatterns, additionalExcluding: excludePatterns)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _compilePatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "compile", "code", warnings: warnings, additionalIncluding: compileBuiltIns, additionalExcluding: excludePatterns)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_preprocessPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _contentPatternsGroup = PatternGroup.Build(rawProject, projectDirectory, projectFilePath, "content", "files", warnings: warnings, additionalIncluding: contentBuiltIns, additionalExcluding: excludePatterns.Concat(_publishExcludePatterns))
                .ExcludeGroup(_compilePatternsGroup)
                .ExcludeGroup(_preprocessPatternsGroup)
                .ExcludeGroup(_sharedPatternsGroup)
                .ExcludeGroup(_resourcePatternsGroup);

            _namedResources = NamedResourceReader.ReadNamedResources(rawProject, projectFilePath);
        }

        public IEnumerable<string> SourceFiles
        {
            get { return _compilePatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> PreprocessSourceFiles
        {
            get { return _preprocessPatternsGroup.SearchFiles(_projectDirectory).Distinct(); }
        }

        public IDictionary<string, string> ResourceFiles
        {
            get
            {
                var resources = _resourcePatternsGroup
                    .SearchFiles(_projectDirectory)
                    .Distinct()
                    .ToDictionary(res => res, res => (string)null);

                NamedResourceReader.ApplyNamedResources(_namedResources, resources);

                return resources;
            }
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
