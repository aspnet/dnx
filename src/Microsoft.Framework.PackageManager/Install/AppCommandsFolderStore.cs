// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Framework.PackageManager
{
    public class AppCommandsFolderStore
    {
        private readonly string _commandsFolder;

        // Key = command; Value = app name
        private IDictionary<string, string> _commands = new Dictionary<string, string>();

        public AppCommandsFolderStore(string commandsFolder)
        {
            _commandsFolder = commandsFolder;
        }

        public IEnumerable<string> Commands
        {
            get
            {
                return _commands.Keys;
            }
        }

        public string FindCommandOwner(string command)
        {
            string appName;
            _commands.TryGetValue(command, out appName);
            return appName;
        }

        public IEnumerable<string> AllCommandsForApp(string appName)
        {
            return _commands
                .Where(cmd => cmd.Value.Equals(appName, StringComparison.OrdinalIgnoreCase))
                .Select(cmd => cmd.Key)
                .ToList();
        }

        public void Load()
        {
            _commands = new Dictionary<string, string>();

            if (Directory.Exists(_commandsFolder))
            {
                var allCommandFiles =  Directory.EnumerateFiles(_commandsFolder, "*.cmd");
                foreach(string commandFile in allCommandFiles)
                {
                    var lines = File.ReadAllLines(commandFile);
                    
                    if (lines.Length != 1)
                    {
                        // The run scripts are just one line so this is not an installed app script
                        continue;
                    }

                    var pathParts = lines[0].Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                    if (pathParts.Length > 2)
                    {
                        _commands.Add(Path.GetFileNameWithoutExtension(commandFile), pathParts[2]);
                    }
                }
            }
        }
    }
}