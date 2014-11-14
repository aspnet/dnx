using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;

namespace CsprojDumper
{
    class Program
    {
        static int Main(string[] args)
        {
            var expandedArgs = ExpandResponseFiles(args);

            if (ShowHelpInformation(expandedArgs))
            {
                return 0;
            }

            var rootProjectFile = GetProjectFile(expandedArgs);
            var dict = GetPropertyDictionary(expandedArgs);

            // 'OutDir' is a mandatory property
            // To simplify the usage of this program, if 'OutDir' is not specified by users, we use a temporary dir
            const string OutDirKey = "OutDir";
            var tempOutDir = string.Empty;
            if (!dict.ContainsKey(OutDirKey))
            {
                tempOutDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempOutDir);
                dict[OutDirKey] = tempOutDir;
            }

            var collection = new ProjectCollection(dict);

            var rootElement = new XElement("root");
            var projectFiles = new List<string> { rootProjectFile };
            for (var index = 0; index != projectFiles.Count; index++)
            {
                var projectFile = projectFiles[index];
                var project = collection.LoadProject(projectFile);
                var projectInstance = project.CreateProjectInstance();
                var buildResult = projectInstance.Build("ResolveReferences", loggers: null);

                var projectElement = new XElement("project");
                projectElement.SetAttributeValue("projectFile", projectFile);
                projectElement.SetAttributeValue("buildResult", buildResult);
                rootElement.Add(projectElement);

                foreach (var itemType in projectInstance.ItemTypes)
                {
                    foreach (var item in projectInstance.GetItems(itemType))
                    {
                        if (item.ItemType == "ReferencePath" && item.HasMetadata("MSBuildSourceProjectFile"))
                        {
                            var referenceProjectFile = item.GetMetadata("MSBuildSourceProjectFile").EvaluatedValue;
                            if (!projectFiles.Contains(referenceProjectFile))
                            {
                                projectFiles.Add(referenceProjectFile);
                            }
                        }

                        var itemElement = new XElement("item");
                        itemElement.SetAttributeValue("itemType", item.ItemType);
                        itemElement.SetAttributeValue("evaluated", item.EvaluatedInclude);
                        projectElement.Add(itemElement);

                        foreach (var metadata in item.Metadata)
                        {
                            var metadataElement = new XElement("metadata");
                            metadataElement.SetAttributeValue("name", metadata.Name);
                            metadataElement.SetAttributeValue("evaluated", metadata.EvaluatedValue);
                            itemElement.Add(metadataElement);
                        }
                    }
                }

                foreach (var property in projectInstance.Properties)
                {
                    var propertyElement = new XElement("property");
                    propertyElement.SetAttributeValue("name", property.Name);
                    propertyElement.SetAttributeValue("evaluated", property.EvaluatedValue);
                    projectElement.Add(propertyElement);
                }
            }

            rootElement.Save(Console.Out);

            if (Directory.Exists(tempOutDir))
            {
                Directory.Delete(tempOutDir, recursive: true);
            }

            return 0;
        }

        private static Dictionary<string, string> GetPropertyDictionary(IEnumerable<string> expandedArgs)
        {
            const string ShortOptionName = "/p:";
            const string LongOptionName = "/property:";

            var dict = new Dictionary<string, string>();

            foreach (var arg in expandedArgs)
            {
                string propertyPair = null;
                if (arg.StartsWith(ShortOptionName))
                {
                    propertyPair = arg.Substring(ShortOptionName.Length);
                }
                else if (arg.StartsWith(LongOptionName))
                {
                    propertyPair = arg.Substring(LongOptionName.Length);
                }

                if (string.IsNullOrEmpty(propertyPair))
                {
                    continue;
                }

                var segments = propertyPair.Split(new[] { '=' }, 2);
                if (segments.Length < 2)
                {
                    throw new InvalidDataException(string.Format("Invalid property assginment format '{0}'", propertyPair));
                }

                dict[segments[0]] = segments[1];
            }

            return dict;
        }

        private static bool ShowHelpInformation(IEnumerable<string> expandedArgs)
        {
            if (expandedArgs.Contains("/h") || expandedArgs.Contains("/help"))
            {
                Console.Out.WriteLine(@"Usage:      {0}.exe [options] [project file]

Options:
  /property:<n>=<v> Set or override global properties. (Short form: /p)
  /target:<target>  Set or override the target to be built. (Short form: /t)",
                    AppDomain.CurrentDomain.FriendlyName);
                return true;
            }
            return false;
        }

        private static string GetProjectFile(IEnumerable<string> expandedArgs)
        {
            string projectFile;
            try
            {
                projectFile = expandedArgs.SingleOrDefault(x => !x.StartsWith("/") && !x.StartsWith("@"));
            }
            catch (InvalidOperationException)
            {
                throw new InvalidDataException("Only one project can be specified");
            }

            if (string.IsNullOrEmpty(projectFile))
            {
                throw new InvalidDataException("No project file was specified");
            }

            return projectFile;
        }

        private static IEnumerable<string> ExpandResponseFiles(string[] args)
        {
            var expandedArgs = new List<string>();
            foreach (var arg in args)
            {
                if (!arg.StartsWith("@"))
                {
                    expandedArgs.Add(arg);
                    continue;
                }

                var file = arg.Substring(1);
                expandedArgs.AddRange(GetArgsFromResponseFile(file));
            }
            return expandedArgs;
        }

        private static IEnumerable<string> GetArgsFromResponseFile(string file)
        {
            if (!File.Exists(file))
            {
                throw new InvalidDataException(
                    string.Format("The specified response file '{0}' doesn't exist", file));
            }

            foreach (var line in File.ReadAllLines(file))
            {
                // Comment start with '#'
                if (line.StartsWith("#"))
                {
                    continue;
                }

                // Split with white-space characters as delimiters
                var args = line.Split(null);
                foreach (var arg in args)
                {
                    yield return arg;
                }
            }
        }
    }
}
