// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.FileSystemGlobbing;
using Newtonsoft.Json.Linq;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.FileGlobbing
{
    public class ProjectFilesCollection
    {
        public static readonly string[] DefaultSourcePatterns = new[] { @"**\*.cs" };
        public static readonly string[] DefaultExcludePatterns = new[] { @"obj\**\*.*", @"bin\**\*.*" };
        public static readonly string[] DefaultBundleExcludePatterns = new[] { @"obj\**\*.*", @"bin\**\*.*", @"**\.*\**" };
        public static readonly string[] DefaultPreprocessPatterns = new[] { @"compiler\preprocess\**\*.cs" };
        public static readonly string[] DefaultSharedPatterns = new[] { @"compiler\shared\**\*.cs" };
        public static readonly string[] DefaultResourcesPatterns = new[] { @"compiler\resources\**\*" };
        public static readonly string[] DefaultContentsPatterns = new[] { @"**\*" };

        private static readonly string PropertyNameCode = "code";
        private static readonly string PropertyNameExclude = "exclude";
        private static readonly string PropertyNameBundleExclude = "bundleExclude";
        private static readonly string PropertyNamePreprocess = "preprocess";
        private static readonly string PropertyNameShared = "shared";
        private static readonly string PropertyNameResources = "resources";
        private static readonly string PropertyNameContent = "files";

        private readonly Matcher _sourcesMatcher;
        private readonly Matcher _preprocessSourcesMatcher;
        private readonly Matcher _sharedSourceMatcher;
        private readonly Matcher _resourcesMatcher;
        private readonly Matcher _contentFilesMatcher;
        private readonly Matcher _bundleExcludeFilesMatcher;

        private readonly string _projectDirectory;
        private readonly JObject _rawProject;

        public ProjectFilesCollection(JObject rawProject, string projectDirectory)
        {
            _projectDirectory = projectDirectory;
            _rawProject = rawProject;

            SourcePatterns = PatternsCollectionHelper.GetPatternsCollection(_rawProject, projectDirectory, PropertyNameCode, DefaultSourcePatterns);
            ExcludePatterns = PatternsCollectionHelper.GetPatternsCollection(_rawProject, projectDirectory, PropertyNameExclude, DefaultExcludePatterns);
            BundleExcludePatterns = PatternsCollectionHelper.GetPatternsCollection(_rawProject, projectDirectory, PropertyNameBundleExclude, DefaultBundleExcludePatterns);
            PreprocessPatterns = PatternsCollectionHelper.GetPatternsCollection(_rawProject, projectDirectory, PropertyNamePreprocess, DefaultPreprocessPatterns);
            SharedPatterns = PatternsCollectionHelper.GetPatternsCollection(_rawProject, projectDirectory, PropertyNameShared, DefaultSharedPatterns);
            ResourcesPatterns = PatternsCollectionHelper.GetPatternsCollection(_rawProject, projectDirectory, PropertyNameResources, DefaultResourcesPatterns);
            ContentsPatterns = PatternsCollectionHelper.GetPatternsCollection(_rawProject, projectDirectory, PropertyNameContent, DefaultContentsPatterns);

            _sourcesMatcher = new Matcher();
            _sourcesMatcher.AddIncludePatterns(SourcePatterns);
            _sourcesMatcher.AddExcludePatterns(PreprocessPatterns, SharedPatterns, ResourcesPatterns, ExcludePatterns);

            _preprocessSourcesMatcher = new Matcher();
            _preprocessSourcesMatcher.AddIncludePatterns(PreprocessPatterns);
            _preprocessSourcesMatcher.AddExcludePatterns(SharedPatterns, ResourcesPatterns, ExcludePatterns);

            _bundleExcludeFilesMatcher = new Matcher();
            _bundleExcludeFilesMatcher.AddIncludePatterns(BundleExcludePatterns);

            _resourcesMatcher = new Matcher();
            _resourcesMatcher.AddIncludePatterns(ResourcesPatterns);

            _sharedSourceMatcher = new Matcher();
            _sharedSourceMatcher.AddIncludePatterns(SharedPatterns);

            _contentFilesMatcher = new Matcher();
            _contentFilesMatcher.AddIncludePatterns(ContentsPatterns);
            _contentFilesMatcher.AddExcludePatterns(SharedPatterns,
                                                    PreprocessPatterns,
                                                    ResourcesPatterns,
                                                    BundleExcludePatterns,
                                                    SourcePatterns);
        }

        public IEnumerable<string> SourceFiles
        {
            get { return _sourcesMatcher.GetResultsInFullPath(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> PreprocessSourceFiles
        {
            get { return _preprocessSourcesMatcher.GetResultsInFullPath(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> BundleExcludeFiles
        {
            get { return _bundleExcludeFilesMatcher.GetResultsInFullPath(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> ResourceFiles
        {
            get { return _resourcesMatcher.GetResultsInFullPath(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> SharedFiles
        {
            get { return _sharedSourceMatcher.GetResultsInFullPath(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> ContentFiles
        {
            get { return _contentFilesMatcher.GetResultsInFullPath(_projectDirectory).Distinct(); }
        }

        public IEnumerable<string> SourcePatterns { get; }

        public IEnumerable<string> ExcludePatterns { get; }

        public IEnumerable<string> BundleExcludePatterns { get; }

        public IEnumerable<string> PreprocessPatterns { get; }

        public IEnumerable<string> SharedPatterns { get; }

        public IEnumerable<string> ResourcesPatterns { get; }

        public IEnumerable<string> ContentsPatterns { get; }

    }
}