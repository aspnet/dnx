using System;
using System.Collections.Generic;

namespace Microsoft.Net.PackageManager.CommandLine
{
    public class CommandOption
    {
        public CommandOption(string template)
        {
            Template = template;

            foreach (var part in Template.Split(new[] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("--"))
                {
                    LongName = part.Substring(2);
                }
                else if (part.StartsWith("-"))
                {
                    ShortName = part.Substring(1);
                }
                else if (part.StartsWith("<") && part.EndsWith(">"))
                {
                    ValueName = part.Substring(1, part.Length - 2);
                }
                else
                {
                    throw new Exception("TODO: invalid template pattern " + template);
                }
            }
        }

        public string Template { get; set; }
        public string ShortName { get; set; }
        public string LongName { get; set; }
        public string ValueName { get; set; }
        public string Description { get; set; }
        public string Value { get; set; }

        public bool TryParse(string value)
        {
            Value = value;
            return true;
        }
    }
}