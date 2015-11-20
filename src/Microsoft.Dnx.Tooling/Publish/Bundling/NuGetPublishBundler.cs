using System;
using System.IO;
using Microsoft.Dnx.Tooling.Building;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish.Bundling
{
    public class NuGetPublishBundler : IPublishBundler
    {
        private readonly string _outputDirectory;

        public NuGetPublishBundler(string outputDirectory)
        {
            _outputDirectory = outputDirectory;
        }

        public bool Bundle(Runtime.Project project, PublishRoot publishRoot, Reports reports)
        {
            var builder = new PackageBuilder();
            builder.InitializeFromProject(project);

            AddPublishedFiles(publishRoot, builder);

            var outputFile = SavePackage(builder);
            reports.Quiet.WriteLine("{0} -> {1}", publishRoot.OutputPath, outputFile);

            return true;
        }

        private static void AddPublishedFiles(PublishRoot publishRoot, PackageBuilder builder)
        {
            foreach (var file in Directory.EnumerateFiles(publishRoot.OutputPath, "*", SearchOption.AllDirectories))
            {
                builder.Files.Add(new PhysicalPackageFile
                {
                    SourcePath = file,
                    TargetPath = file.Replace(publishRoot.OutputPath, "").Trim('\\', '/')
                });
            }
        }

        private string SavePackage(PackageBuilder builder)
        {
            var outputFile = Path.GetFullPath(Path.Combine(_outputDirectory, $"{builder.Id}.{builder.Version}.nupkg"));
            using (var fs = File.Create(outputFile))
            {
                builder.Save(fs);
            }
            return outputFile;
        }
    }
}
