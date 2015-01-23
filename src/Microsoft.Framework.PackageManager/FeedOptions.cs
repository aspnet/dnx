// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Runtime.Common.CommandLine;
using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager
{
    public class FeedOptions
    {
        public IList<string> FallbackSources
        {
            get
            {
                return FallbackSourceOptions.Values;
            }
        }

        public bool IgnoreFailedSources
        {
            get
            {
                return IgnoreFailedSourcesOptions.HasValue();
            }
        }
        public bool NoCache
        {
            get
            {
                return NoCacheOptions.HasValue();
            }
        }
        public string TargetPackagesFolder
        {
            get
            {
                return TargetPackagesFolderOptions.Value();
            }
        }
        public string Proxy
        {
            get
            {
                return ProxyOptions.Value();
            }
        }
        public IList<string> Sources
        {
            get
            {
                return SourceOptions.Values;
            }
        }

        public bool Quiet
        {
            get
            {
                return QuietOptions.HasValue();
            }
        }

        internal CommandOption FallbackSourceOptions { get; private set; }
        internal CommandOption IgnoreFailedSourcesOptions { get; private set; }
        internal CommandOption NoCacheOptions { get; private set; }
        internal CommandOption TargetPackagesFolderOptions { get; private set; }
        internal CommandOption ProxyOptions { get; private set; }
        internal CommandOption SourceOptions { get; private set; }
        internal CommandOption QuietOptions { get; private set; }

        internal static FeedOptions Add(CommandLineApplication c)
        {
            var options = new FeedOptions();

            options.SourceOptions = c.Option(
                "-s|--source <FEED>",
                "A list of packages sources to use for this command",
                CommandOptionType.MultipleValue);

            options.FallbackSourceOptions = c.Option(
                "-f|--fallbacksource <FEED>",
                "A list of packages sources to use as a fallback",
                CommandOptionType.MultipleValue);

            options.ProxyOptions = c.Option(
                "-p|--proxy <ADDRESS>",
                "The HTTP proxy to use when retrieving packages",
                CommandOptionType.SingleValue);

            options.NoCacheOptions = c.Option(
                "--no-cache",
                "Do not use local cache",
                CommandOptionType.NoValue);

            options.TargetPackagesFolderOptions = c.Option(
                "--packages",
                "Path to restore packages",
                CommandOptionType.SingleValue);

            options.IgnoreFailedSourcesOptions = c.Option(
                "--ignore-failed-sources",
                "Ignore failed remote sources if there are local packages meeting version requirements",
                CommandOptionType.NoValue);

            options.QuietOptions = c.Option(
                "--quiet", "Do not show output such as HTTP request/cache information",
                CommandOptionType.NoValue);

            return options;
        }
    }
}
