using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Microsoft.Net.Runtime.FileSystem;

namespace Microsoft.Net.Runtime.Loader.MSBuildProject
{
#if NET45
    public class MSBuildProjectAssemblyLoader : IAssemblyLoader
    {
        private readonly string _solutionDir;
        private readonly IFileWatcher _watcher;

        private readonly static string[] _msBuildPaths = new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"MSBuild\12.0\Bin\MSBuild.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework\v4.0.30319\MSBuild.exe")
        };

        public MSBuildProjectAssemblyLoader(string solutionDir, IFileWatcher watcher)
        {
            _solutionDir = solutionDir;
            _watcher = watcher;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            string name = loadContext.AssemblyName;

            string targetDir = Path.Combine(_solutionDir, name);

            // Bail if there's a project settings file
            if (Project.HasProjectFile(targetDir))
            {
                return null;
            }

            string projectFile = Path.Combine(targetDir, name + ".csproj");
            
            if (!System.IO.File.Exists(projectFile))
            {
                // There's a solution so check for a project one deeper
                if (System.IO.File.Exists(Path.Combine(targetDir, name + ".sln")))
                {
                    // Is there a project file here?
                    projectFile = Path.Combine(targetDir, name, name + ".csproj");

                    if (!System.IO.File.Exists(projectFile))
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            WatchProject(projectFile);

            string projectDir = Path.GetDirectoryName(projectFile);

            foreach (var exePath in _msBuildPaths)
            {
                if (!System.IO.File.Exists(exePath))
                {
                    continue;
                }

                var executable = new Executable(exePath, projectDir);

                string outputFile = null;
                var process = executable.Execute(line =>
                {
                    // Look for {project} -> {outputPath}
                    int index = line.IndexOf('-');

                    if (index != -1 && index + 1 < line.Length && line[index + 1] == '>')
                    {
                        string projectName = line.Substring(0, index).Trim();
                        if (projectName.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            outputFile = line.Substring(index + 2).Trim();
                        }
                    }

                    return true;
                },
                _ => true,
                Encoding.UTF8,
                projectFile + " /m");

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    // REVIEW: Should this throw?
                    return null;
                }

                return new AssemblyLoadResult(Assembly.LoadFile(outputFile));
            }

            return null;
        }

        private void WatchProject(string projectFile)
        {
            // We're already watching this file
            if (!_watcher.WatchFile(projectFile))
            {
                return;
            }

            string projectDir = Path.GetDirectoryName(projectFile);

            XDocument document = null;
            using (var stream = System.IO.File.OpenRead(projectFile))
            {
                document = XDocument.Load(stream);
            }

            foreach (var contentItem in GetSourceFilenames(document))
            {
                var path = Path.Combine(projectDir, contentItem);
                _watcher.WatchFile(Path.GetFullPath(path));
            }

            // Watch project references
            foreach (var projectReferencePath in GetProjectReferences(document))
            {
                string path = Path.GetFullPath(Path.Combine(projectDir, projectReferencePath));

                WatchProject(path);
            }
        }

        private static string GetAssemblyName(XDocument document)
        {
            return document
                .Elements(ns("Project"))
                .Elements(ns("PropertyGroup"))
                .Elements(ns("AssemblyName"))
                .Single()
                .Value;
        }

        private static IEnumerable<string> GetSourceFilenames(XDocument document)
        {
            return document
                .Elements(ns("Project"))
                .Elements(ns("ItemGroup"))
                .Elements(ns("Compile"))
                .Attributes("Include")
                .Select(c => c.Value);
        }

        private static IEnumerable<string> GetProjectReferences(XDocument document)
        {
            return document
                .Elements(ns("Project"))
                .Elements(ns("ItemGroup"))
                .Elements(ns("ProjectReference"))
                .Attributes("Include")
                .Select(c => c.Value);
        }

        private static XName ns(string name)
        {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }
    }
#endif
}
