// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.Common.CommandLine
{
    internal class CommandOptions
    {
        private readonly Dictionary<string, List<string>> _parsedOptions;
        private readonly Dictionary<string, CommandOptionType> _validOptions;

        public CommandOptions(Dictionary<string, CommandOptionType> validOptions,
                              Dictionary<string, List<string>> parsedOptions,
                              IList<string> remainingArgs)
        {
            _validOptions = validOptions;
            _parsedOptions = parsedOptions;
            RemainingArgs = remainingArgs;
        }

        public IList<string> RemainingArgs { get; private set; }

        public bool HasOption(string option)
        {
            return _parsedOptions.ContainsKey(option);
        }

        public string GetValue(string option)
        {
            List<string> values;
            if (_parsedOptions.TryGetValue(option, out values))
            {
                switch (_validOptions[option])
                {
                    case CommandOptionType.MultipleValue:
                    case CommandOptionType.SingleValue:
                        return values[0];
                    case CommandOptionType.NoValue:
                        throw new InvalidOperationException(option + " does not allow values");
                }
            }

            return null;
        }

        public IEnumerable<string> GetValues(string option)
        {
            List<string> values;
            if (_parsedOptions.TryGetValue(option, out values))
            {
                switch (_validOptions[option])
                {
                    case CommandOptionType.MultipleValue:
                        return values;
                    case CommandOptionType.SingleValue:
                    case CommandOptionType.NoValue:
                        throw new InvalidOperationException(option + " does not allow multiple values");
                }
            }

            return null;
        }
    }
}
