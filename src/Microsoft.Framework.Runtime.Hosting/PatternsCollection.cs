// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime.FileGlobbing
{
    internal static class PatternsCollectionHelper
    {
        private static readonly char[] PatternSeparator = new[] { ';' };

        public static IEnumerable<string> GetPatternsCollection(JObject rawProject, string projectDirectory, string propertyName, string[] defaultPatterns)
        {
            var token = rawProject[propertyName];
            if (token == null)
            {
                return defaultPatterns;
            }

            if (token.Type == JTokenType.Null)
            {
                return CreateCollection(projectDirectory);
            }

            if (token.Type == JTokenType.String)
            {
                return CreateCollection(projectDirectory, token.Value<string>());
            }

            if (token.Type == JTokenType.Array)
            {
                return CreateCollection(projectDirectory, token.ValueAsArray<string>());
            }

            throw new InvalidOperationException("Project json doesn't contain qualified token for property " + propertyName + ".");
        }

        private static IEnumerable<string> CreateCollection(string projectDirectory, params string[] patternsStrings)
        {
            var patterns = patternsStrings.SelectMany(patternsString => GetSourcesSplit(patternsString));

            foreach (var pattern in patterns)
            {
                if (Path.IsPathRooted(pattern))
                {
                    throw new InvalidOperationException(string.Format("Patten {0} is a rooted path, which is not supported.", pattern));
                }
            }

            return new List<string>(patterns.Select(pattern => FolderToPattern(pattern, projectDirectory)));
        }

        private static IEnumerable<string> GetSourcesSplit(string sourceDescription)
        {
            if (string.IsNullOrEmpty(sourceDescription))
            {
                return Enumerable.Empty<string>();
            }

            return sourceDescription.Split(PatternSeparator, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string FolderToPattern(string candidate, string projectDir)
        {
            // This conversion is needed to support current template

            // If it's already a pattern, no change is needed
            if (candidate.Contains('*'))
            {
                return candidate;
            }

            // If the given string ends with a path separator, or it is an existing directory
            // we convert this folder name to a pattern matching all files in the folder
            if (candidate.EndsWith(@"\") ||
                candidate.EndsWith("/") ||
                Directory.Exists(Path.Combine(projectDir, candidate)))
            {
                return Path.Combine(candidate, "**", "*");
            }

            // Otherwise, it represents a single file
            return candidate;
        }
    }
}