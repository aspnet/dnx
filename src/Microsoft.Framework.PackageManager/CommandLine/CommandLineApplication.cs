using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.PackageManager.CommandLine
{
    public class CommandLineApplication : CommandInfo
    {
        public CommandLineApplication()
        {
        }

        public new CommandLineApplication Command(string name, Action<CommandInfo> configuration)
        {
            return (CommandLineApplication)base.Command(name, configuration);
        }

        public int Execute(params string[] args)
        {
            CommandInfo command = this;
            CommandOption option = null;
            IEnumerator<CommandArgument> arguments = null;

            List<string> remainingArguments = new List<string>();

            foreach (var arg in args)
            {
                var processed = false;
                if (!processed && option == null)
                {
                    string[] longOption = null;
                    string[] shortOption = null;
                    if (arg.StartsWith("/"))
                    {
                        longOption = arg.Substring(1).Split(new[] { ':', '=' }, 2);
                    }
                    else if (arg.StartsWith("--"))
                    {
                        longOption = arg.Substring(2).Split(new[] { ':', '=' }, 2);
                    }
                    else if (arg.StartsWith("-"))
                    {
                        shortOption = arg.Substring(1).Split(new[] { ':', '=' }, 2);
                    }
                    if (longOption != null)
                    {
                        processed = true;
                        option = command.GetAllOptions().SingleOrDefault(opt => string.Equals(opt.LongName, longOption[0], StringComparison.Ordinal));
                        if (option == null)
                        {
                            throw new Exception(String.Format("TODO: unknown option '{0}'", arg));
                        }
                        if (longOption.Length == 2)
                        {
                            if (!option.TryParse(longOption[1]))
                            {
                                throw new Exception(String.Format("TODO: unexpected value '{0}' for option '{1}'", longOption[1], option.LongName));
                            }
                            option = null;
                        }
                        else if (option.ValueName == null)
                        {
                            option.Value = "on";
                            option = null;
                        }
                    }
                    if (shortOption != null)
                    {
                        processed = true;
                        option = command.GetAllOptions().SingleOrDefault(opt => string.Equals(opt.ShortName, shortOption[0], StringComparison.Ordinal));
                        if (option == null)
                        {
                            throw new Exception(String.Format("TODO: unknown option '{0}'", arg));
                        }
                        if (shortOption.Length == 2)
                        {
                            if (!option.TryParse(shortOption[1]))
                            {
                                throw new Exception(String.Format("TODO: unexpected value '{0}' for option '{1}'", shortOption[1], option.LongName));
                            }
                            option = null;
                        }
                        else if (option.ValueName == null)
                        {
                            option.Value = "on";
                            option = null;
                        }
                    }
                }

                if (!processed && option != null)
                {
                    processed = true;
                    if (!option.TryParse(arg))
                    {
                        throw new Exception(String.Format("TODO: unexpected value '{0}' for option '{1}'", arg, option.LongName));
                    }
                    option = null;
                }

                if (!processed && arguments == null)
                {
                    foreach (var subcommand in command.Commands)
                    {
                        if (subcommand.Name == arg)
                        {
                            processed = true;
                            command = subcommand;
                        }
                    }
                }
                if (!processed)
                {
                    if (arguments == null)
                    {
                        arguments = ((IEnumerable<CommandArgument>)command.Arguments).GetEnumerator();
                    }
                    if (arguments.MoveNext())
                    {
                        processed = true;
                        arguments.Current.Value = arg;
                    }
                }
                if (!processed)
                {
                    throw new Exception(string.Format("TODO: unexpected argument '{0}'", arg));
                }
            }

            if (option != null)
            {
                throw new Exception(string.Format("TODO: missing value for option"));
            }
            return command.Invoke();
        }
    }
}
