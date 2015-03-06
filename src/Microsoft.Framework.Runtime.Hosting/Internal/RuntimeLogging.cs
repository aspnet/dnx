// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Logging;

namespace Microsoft.Framework.Runtime.Internal
{
    public static class RuntimeLogging
    {
        private static ILoggerFactory _loggerFactory;
        private static Func<string, LogLevel, bool> _filter;

        public static Func<string, LogLevel, bool> Filter { get { return _filter; } }
        public static bool IsEnabled { get { return _filter != null; } }

        public static void Initialize(string traceConfigurationString, Func<ILoggerFactory> loggerFactoryFactory)
        {
            // Parse KRE_TRACE
            if (string.IsNullOrEmpty(traceConfigurationString))
            {
                _loggerFactory = NullLoggerFactory.Instance;
                return;
            }
            else if (string.Equals(traceConfigurationString, "1", StringComparison.OrdinalIgnoreCase))
            {
                // Support for legacy KRE_TRACE=1 value
                _filter = (_, level) => level >= LogLevel.Information;
            }
            else
            {
                ParseTrace(traceConfigurationString);
            }
            _loggerFactory = loggerFactoryFactory();
        }

        public static ILogger Logger<T>()
        {
            return _loggerFactory.Create<T>();
        }

        public static ILogger Logger(string name)
        {
            return _loggerFactory.Create(name);
        }

        private static void ParseTrace(string trace)
        {
            var segments = trace.Split(';');
            IList<Tuple<string, LogLevel?>> filters = new List<Tuple<string, LogLevel?>>();
            foreach (var segment in segments)
            {
                var subsegments = segment.Split(':');
                if (subsegments.Length == 1)
                {
                    // [category]
                    // Enable the category at the default leve (Information)
                    filters.Add(Tuple.Create(subsegments[0], (LogLevel?)LogLevel.Information));
                }
                else if (subsegments.Length == 2)
                {
                    LogLevel level;
                    if (string.IsNullOrWhiteSpace(subsegments[1]) || string.Equals(subsegments[1], "off", StringComparison.OrdinalIgnoreCase))
                    {
                        // [category]:
                        // Disable the category
                        filters.Add(Tuple.Create(subsegments[0], (LogLevel?)null));
                    }
                    else if (Enum.TryParse(subsegments[1], true, out level))
                    {
                        // [category]:[level]
                        filters.Add(Tuple.Create(subsegments[0], (LogLevel?)level));
                    }
                    // Unable to parse. Just ignore it :(
                }
                // Unable to parse. Just ignore it :(
            }

            // Create the filter
            _filter = (name, level) =>
            {
                var applicableFilter = filters.FirstOrDefault(f => IsMatch(f.Item1, name));
                if (applicableFilter != null)
                {
                    return level >= applicableFilter.Item2;
                }
                return false;
            };
        }

        private static bool IsMatch(string filter, string name)
        {
            return string.Equals(filter, "*", StringComparison.Ordinal) ||
                name.StartsWith(filter, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}