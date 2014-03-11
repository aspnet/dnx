using System;
using System.Collections.Generic;

namespace Microsoft.Net.Runtime.Common.CommandLine
{
    internal class CommandLineParser
    {
        // --opt1 --opt2 value --opt2 value {args}
        public void ParseOptions(string[] args,
                                 Dictionary<string, CommandOptionType> validOptions,
                                 out CommandOptions options)
        {
            var parsed = new Dictionary<string, List<string>>();
            string currentOpt = null;
            int index = 0;

            for (; index < args.Length; index++)
            {
                var arg = args[index];
                if (arg.StartsWith("--"))
                {
                    if (!String.IsNullOrEmpty(currentOpt))
                    {
                        throw new ArgumentException("Expected value for " + arg);
                    }

                    var option = arg.Substring(2);

                    CommandOptionType optType;
                    if (!validOptions.TryGetValue(option, out optType))
                    {
                        throw new ArgumentException("Invalid option " + arg);
                    }

                    List<string> existingValues;

                    switch (optType)
                    {
                        case CommandOptionType.MultipleValue:
                            if (!parsed.TryGetValue(option, out existingValues))
                            {
                                existingValues = new List<string>();
                                parsed[option] = existingValues;
                            }

                            currentOpt = option;
                            break;
                        case CommandOptionType.SingleValue:
                            if (parsed.TryGetValue(option, out existingValues))
                            {
                                throw new ArgumentException("Multiple values not allowed for " + arg);
                            }
                            else
                            {
                                existingValues = new List<string>();
                                parsed[option] = existingValues;
                                currentOpt = option;
                            }

                            break;
                        case CommandOptionType.NoValue:
                            parsed[option] = new List<string>();
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    if (String.IsNullOrEmpty(currentOpt))
                    {
                        break;
                    }

                    parsed[currentOpt].Add(arg);
                    currentOpt = null;
                }
            }

            if (!String.IsNullOrEmpty(currentOpt))
            {
                throw new ArgumentException("Expected value for --" + currentOpt);
            }

            options = new CommandOptions(validOptions, parsed, new ArraySegment<string>(args, index, args.Length - index));
        }
    }
}
