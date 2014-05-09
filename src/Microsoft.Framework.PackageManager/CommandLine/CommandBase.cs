// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager.CommandLine
{
    public class CommandInfo
    {
        public CommandInfo()
        {
            Options = new List<CommandOption>();
            Arguments = new List<CommandArgument>();
            Commands = new List<CommandInfo>();
        }

        public CommandInfo Parent { get; set; }
        public string Name { get; set; }
        public string Syntax { get; set; }
        public string Description { get; set; }
        public List<CommandOption> Options { get; private set; }
        public List<CommandArgument> Arguments { get; private set; }
        public Func<int> Invoke { get; set; }

        public List<CommandInfo> Commands { get; private set; }

        public CommandInfo Command(string name, Action<CommandInfo> configuration)
        {
            var command = new CommandInfo() { Name = name, Parent = this };
            Commands.Add(command);
            configuration(command);
            return this;
        }

        public CommandOption Option(string template, string description)
        {
            return Option(template, description, _ => { });
        }

        public CommandOption Option(string template, string description, Action<CommandOption> configuration)
        {
            var option = new CommandOption(template) { Description = description };
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

        public IEnumerable<CommandOption> GetAllOptions()
        {
            for (var scan = this; scan != null; scan = scan.Parent)
            {
                foreach (var option in scan.Options)
                {
                    yield return option;
                }
            }
        }

        public void OnExecute(Func<int> invoke)
        {
            Invoke = invoke;
        }
    }
}
