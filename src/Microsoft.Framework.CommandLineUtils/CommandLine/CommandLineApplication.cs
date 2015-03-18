// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Framework.Runtime.Common.CommandLine
{
    internal class CommandLineApplication
    {
        // Indicates whether the parser should throw an exception when it runs into an unexpected argument.
        // If this field is set to false, the parser will stop parsing when it sees an unexpected argument, and all
        // remaining arguments, including the first unexpected argument, will be stored in RemainingArguments property.
        private readonly bool _throwOnUnexpectedArg;

        public CommandLineApplication(bool throwOnUnexpectedArg = true)
        {
            _throwOnUnexpectedArg = throwOnUnexpectedArg;
            Options = new List<CommandOption>();
            Arguments = new List<CommandArgument>();
            Commands = new List<CommandLineApplication>();
            RemainingArguments = new List<string>();
            Invoke = () => 0;
        }

        public CommandLineApplication Parent { get; set; }
        public string Name { get; set; }
        public string Syntax { get; set; }
        public string Description { get; set; }
        public List<CommandOption> Options { get; private set; }
        public CommandOption OptionHelp { get; private set; }
        public CommandOption OptionVersion { get; private set; }
        public List<CommandArgument> Arguments { get; private set; }
        public List<string> RemainingArguments { get; private set; }
        public bool IsShowingInformation { get; protected set; }  // Is showing help or version?
        public Func<int> Invoke { get; set; }
        public Func<string> VersionGetter { get; set; }

        public List<CommandLineApplication> Commands { get; private set; }

        public CommandLineApplication Command(string name, Action<CommandLineApplication> configuration,
            bool addHelpCommand = true, bool throwOnUnexpectedArg = true)
        {
            var command = new CommandLineApplication(throwOnUnexpectedArg) { Name = name, Parent = this };
            Commands.Add(command);
            configuration(command);

            if (addHelpCommand)
            {
                if (HasHelpCommand())
                {
                    // Already added before
                    return this;
                }

                Command("help", c =>
                {
                    c.Description = "Show help information";

                    var argCommand = c.Argument("[command]", "Command that help information explains");

                    c.OnExecute(() =>
                    {
                        ShowHelp(argCommand.Value);
                        return 0;
                    });
                },
                addHelpCommand: false);
            }

            return command;
        }

        public CommandOption Option(string template, string description, CommandOptionType optionType)
        {
            return Option(template, description, optionType, _ => { });
        }

        public CommandOption Option(string template, string description, CommandOptionType optionType, Action<CommandOption> configuration)
        {
            var option = new CommandOption(template, optionType) { Description = description };
            Options.Add(option);
            configuration(option);
            return option;
        }

        public CommandArgument Argument(string name, string description)
        {
            return Argument(name, description, _ => { });
        }

        public CommandArgument Argument(string name, string description, Action<CommandArgument> configuration)
        {
            var argument = new CommandArgument { Name = name, Description = description };
            Arguments.Add(argument);
            configuration(argument);
            return argument;
        }

        public void OnExecute(Func<int> invoke)
        {
            Invoke = invoke;
        }
        
        public void OnExecute(Func<Task<int>> invoke)
        {
            Invoke = () => invoke().Result;
        }

        public int Execute(params string[] args)
        {
            CommandLineApplication command = this;
            CommandOption option = null;
            IEnumerator<CommandArgument> arguments = null;

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                var processed = false;
                if (!processed && option == null)
                {
                    string[] longOption = null;
                    string[] shortOption = null;

                    if (arg.StartsWith("--"))
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
                        option = command.Options.SingleOrDefault(opt => string.Equals(opt.LongName, longOption[0], StringComparison.Ordinal));

                        if (option == null)
                        {
                            HandleUnexpectedArg(command, args, index, argTypeName: "option");
                            break;
                        }

                        // If we find a help/version option, show information and stop parsing
                        if (command.OptionHelp == option)
                        {
                            command.ShowHelp();
                            return 0;
                        }
                        else if (command.OptionVersion == option)
                        {
                            command.ShowVersion();
                            return 0;
                        }

                        if (longOption.Length == 2)
                        {
                            if (!option.TryParse(longOption[1]))
                            {
                                command.ShowHint();
                                throw new Exception(string.Format("TODO: Error: unexpected value '{0}' for option '{1}'", longOption[1], option.LongName));
                            }
                            option = null;
                        }
                        else if (option.OptionType == CommandOptionType.NoValue)
                        {
                            // No value is needed for this option
                            option.TryParse(null);
                            option = null;
                        }
                    }
                    if (shortOption != null)
                    {
                        processed = true;
                        option = command.Options.SingleOrDefault(opt => string.Equals(opt.ShortName, shortOption[0], StringComparison.Ordinal));

                        // If not a short option, try symbol option
                        if (option == null)
                        {
                            option = command.Options.SingleOrDefault(opt => string.Equals(opt.SymbolName, shortOption[0], StringComparison.Ordinal));
                        }

                        if (option == null)
                        {
                            HandleUnexpectedArg(command, args, index, argTypeName: "option");
                            break;
                        }

                        // If we find a help/version option, show information and stop parsing
                        if (command.OptionHelp == option)
                        {
                            command.ShowHelp();
                            return 0;
                        }
                        else if (command.OptionVersion == option)
                        {
                            command.ShowVersion();
                            return 0;
                        }

                        if (shortOption.Length == 2)
                        {
                            if (!option.TryParse(shortOption[1]))
                            {
                                command.ShowHint();
                                throw new Exception(string.Format("TODO: Error: unexpected value '{0}' for option '{1}'", shortOption[1], option.LongName));
                            }
                            option = null;
                        }
                        else if (option.OptionType == CommandOptionType.NoValue)
                        {
                            // No value is needed for this option
                            option.TryParse(null);
                            option = null;
                        }
                    }
                }

                if (!processed && option != null)
                {
                    processed = true;
                    if (!option.TryParse(arg))
                    {
                        command.ShowHint();
                        throw new Exception(string.Format("TODO: Error: unexpected value '{0}' for option '{1}'", arg, option.LongName));
                    }
                    option = null;
                }

                if (!processed && arguments == null)
                {
                    var currentCommand = command;
                    foreach (var subcommand in command.Commands)
                    {
                        if (string.Equals(subcommand.Name, arg, StringComparison.OrdinalIgnoreCase))
                        {
                            processed = true;
                            command = subcommand;
                            break;
                        }
                    }

                    // If we detect a subcommand
                    if (command != currentCommand)
                    {
                        processed = true;
                    }
                }
                if (!processed)
                {
                    if (arguments == null)
                    {
                        arguments = command.Arguments.GetEnumerator();
                    }
                    if (arguments.MoveNext())
                    {
                        processed = true;
                        arguments.Current.Value = arg;
                    }
                }
                if (!processed)
                {
                    HandleUnexpectedArg(command, args, index, argTypeName: "argument");
                    break;
                }
            }

            if (option != null)
            {
                command.ShowHint();
                throw new Exception(string.Format("TODO: Error: missing value for option"));
            }

            return command.Invoke();
        }

        // Helper method that adds a help option
        public CommandOption HelpOption(string template)
        {
            // Help option is special because we stop parsing once we see it
            // So we store it separately for further use
            OptionHelp = Option(template, "Show help information", CommandOptionType.NoValue);

            return OptionHelp;
        }

        public CommandOption VersionOption(string template, string version)
        {
            return VersionOption(template, () => version);
        }

        // Helper method that adds a version option
        public CommandOption VersionOption(string template, Func<string> versionGetter)
        {
            // Version option is special because we stop parsing once we see it
            // So we store it separately for further use
            OptionVersion = Option(template, "Show version information", CommandOptionType.NoValue);
            VersionGetter = versionGetter;

            return OptionVersion;
        }

        // Show short hint that reminds users to use help option
        public void ShowHint()
        {
            if (OptionHelp != null)
            {
                Console.WriteLine(string.Format("Specify --{0} for a list of available options and commands.", OptionHelp.LongName));
            }
        }

        // Show full help
        public void ShowHelp(string commandName = null)
        {
            var headerBuilder = new StringBuilder("Usage:");
            for (var cmd = this; cmd != null; cmd = cmd.Parent)
            {
                cmd.IsShowingInformation = true;
                headerBuilder.Insert(6, string.Format(" {0}", cmd.Name));
            }

            var version = string.Format("Microsoft .NET Execution environment v{0}{1}{1}", VersionGetter(), Environment.NewLine);
            CommandLineApplication target;

            if (commandName == null || string.Equals(Name, commandName, StringComparison.OrdinalIgnoreCase))
            {
                target = this;
            }
            else
            {
                target = Commands.SingleOrDefault(cmd => string.Equals(cmd.Name, commandName, StringComparison.OrdinalIgnoreCase));
                headerBuilder.AppendFormat(" {0}", commandName);
            }

            var optionsBuilder = new StringBuilder();
            var commandsBuilder = new StringBuilder();
            var argumentsBuilder = new StringBuilder();

            if (target.Arguments.Any())
            {
                headerBuilder.Append(" [arguments]");

                argumentsBuilder.AppendLine();
                argumentsBuilder.AppendLine("Arguments:");
                var maxArgLen = MaxArgumentLength(target.Arguments);
                var outputFormat = string.Format("  {{0, -{0}}}{{1}}", maxArgLen + 2);
                foreach (var arg in target.Arguments)
                {
                    argumentsBuilder.AppendFormat(outputFormat, arg.Name, arg.Description);
                    argumentsBuilder.AppendLine();
                }
            }

            if (target.Options.Any())
            {
                headerBuilder.Append(" [options]");

                optionsBuilder.AppendLine();
                optionsBuilder.AppendLine("Options:");
                var maxOptLen = MaxOptionTemplateLength(target.Options);
                var outputFormat = string.Format("  {{0, -{0}}}{{1}}", maxOptLen + 2);
                foreach (var opt in target.Options)
                {
                    optionsBuilder.AppendFormat(outputFormat, opt.Template, opt.Description);
                    optionsBuilder.AppendLine();
                }
            }

            if (target.Commands.Any())
            {
                headerBuilder.Append(" [command]");

                commandsBuilder.AppendLine();
                commandsBuilder.AppendLine("Commands:");
                var maxCmdLen = MaxCommandLength(target.Commands);
                var outputFormat = string.Format("  {{0, -{0}}}{{1}}", maxCmdLen + 2);
                foreach (var cmd in target.Commands)
                {
                    commandsBuilder.AppendFormat(outputFormat, cmd.Name, cmd.Description);
                    commandsBuilder.AppendLine();
                }

                if (HasHelpCommand())
                {
                    commandsBuilder.AppendLine();
                    commandsBuilder.AppendFormat("Use \"{0} help [command]\" for more information about a command.", Name);
                    commandsBuilder.AppendLine();
                }
            }
            headerBuilder.AppendLine();
            Console.Write("{0}{1}{2}{3}{4}", version, headerBuilder, argumentsBuilder, optionsBuilder, commandsBuilder);
        }

        public void ShowVersion()
        {
            for (var cmd = this; cmd != null; cmd = cmd.Parent)
            {
                cmd.IsShowingInformation = true;
            }

            Console.WriteLine(VersionGetter());
        }

        private bool HasHelpCommand()
        {
            var helpCmd = Commands.SingleOrDefault(cmd => string.Equals("help", cmd.Name, StringComparison.OrdinalIgnoreCase));
            return helpCmd != null;
        }

        private int MaxOptionTemplateLength(IEnumerable<CommandOption> options)
        {
            var maxLen = 0;
            foreach (var opt in options)
            {
                maxLen = opt.Template.Length > maxLen ? opt.Template.Length : maxLen;
            }
            return maxLen;
        }

        private int MaxCommandLength(IEnumerable<CommandLineApplication> commands)
        {
            var maxLen = 0;
            foreach (var cmd in commands)
            {
                maxLen = cmd.Name.Length > maxLen ? cmd.Name.Length : maxLen;
            }
            return maxLen;
        }

        private int MaxArgumentLength(IEnumerable<CommandArgument> arguments)
        {
            var maxLen = 0;
            foreach (var arg in arguments)
            {
                maxLen = arg.Name.Length > maxLen ? arg.Name.Length : maxLen;
            }
            return maxLen;
        }

        private void HandleUnexpectedArg(CommandLineApplication command, string[] args, int index, string argTypeName)
        {
            if (command._throwOnUnexpectedArg)
            {
                command.ShowHint();
                throw new Exception(string.Format("TODO: Error: unrecognized {0} '{1}'", argTypeName, args[index]));
            }
            else
            {
                // All remaining arguments are stored for further use
                command.RemainingArguments.AddRange(new ArraySegment<string>(args, index, args.Length - index));
            }
        }
    }
}
