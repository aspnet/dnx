// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Runtime.Common.CommandLine;
using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager
{
    public class FeedOptions
    {
        public IList<string> FallbackSources { get; set; } = new List<string>();

        public bool IgnoreFailedSources { get; set; }

        public bool NoCache { get; set; }

        public string TargetPackagesFolder { get; set; }

        public string Proxy { get; set; }

        public IList<string> Sources { get; set; } = new List<string>();

        public bool Quiet { get; set; }

        /// <summary>
        /// Gets or sets a flag that determines if restore is performed on multiple project.json files in parallel.
        /// </summary>
        public bool Parallel { get; set; }
    }

    public class FeedCommandLineOptions
    {
        internal CommandOption FallbackSourceOptions { get; private set; }
        internal CommandOption IgnoreFailedSourcesOptions { get; private set; }
        internal CommandOption NoCacheOptions { get; private set; }
        internal CommandOption TargetPackagesFolderOptions { get; private set; }
        internal CommandOption ProxyOptions { get; private set; }
        internal CommandOption SourceOptions { get; private set; }
        internal CommandOption QuietOptions { get; private set; }
        internal CommandOption ParallelOptions { get; private set; }

        internal static FeedCommandLineOptions Add(CommandLineApplication app)
        {
            var options = new FeedCommandLineOptions();

            options.SourceOptions = app.Option(
                "-s|--source <FEED>",
                "A list of packages sources to use for this command",
                CommandOptionType.MultipleValue);

            options.FallbackSourceOptions = app.Option(
                "-f|--fallbacksource <FEED>",
                "A list of packages sources to use as a fallback",
                CommandOptionType.MultipleValue);

            options.ProxyOptions = app.Option(
                "-p|--proxy <ADDRESS>",
                "The HTTP proxy to use when retrieving packages",
                CommandOptionType.SingleValue);

            options.NoCacheOptions = app.Option(
                "--no-cache",
                "Do not use local cache",
                CommandOptionType.NoValue);

            options.TargetPackagesFolderOptions = app.Option(
                "--packages",
                "Path to restore packages",
                CommandOptionType.SingleValue);

            options.IgnoreFailedSourcesOptions = app.Option(
                "--ignore-failed-sources",
                "Ignore failed remote sources if there are local packages meeting version requirements",
                CommandOptionType.NoValue);

            options.QuietOptions = app.Option(
                "--quiet", "Do not show output such as HTTP request/cache information",
                CommandOptionType.NoValue);

            options.ParallelOptions = app.Option("--parallel",
                "Restores in parallel when more than one project.json is discovered.",
                CommandOptionType.NoValue);

            return options;
        }

        public FeedOptions GetOptions()
        {
            var options = new FeedOptions();

            options.FallbackSources = FallbackSourceOptions.Values ?? options.FallbackSources;
            options.Sources = SourceOptions.Values ?? options.Sources;
            options.IgnoreFailedSources = IgnoreFailedSourcesOptions.HasValue();
            options.NoCache = NoCacheOptions.HasValue();
            options.Parallel = ParallelOptions.HasValue();
            options.Quiet = QuietOptions.HasValue();
            options.Proxy = ProxyOptions.Value();
            options.TargetPackagesFolder = TargetPackagesFolderOptions.Value();

            return options;
        }
    }
}
