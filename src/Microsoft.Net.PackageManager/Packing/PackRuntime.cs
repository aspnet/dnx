using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Loader.NuGet;
using NuGet;
using System.Security.Cryptography;

namespace Microsoft.Net.PackageManager.Packing
{
    public class PackRuntime
    {
        //private readonly NuGetDependencyResolver _nugetDependencyResolver;
        //private readonly Library _library;
        //private readonly FrameworkName _frameworkName;
        string _kreNupkgPath;

        public PackRuntime(
            string kreNupkgPath)
        {
            _kreNupkgPath = kreNupkgPath;
        }

        public string Name { get; set; }
        public SemanticVersion Version { get; set; }
        public string TargetPath { get; set; }

        public void Emit(PackRoot root)
        {
            Name = Path.GetFileName(Path.GetDirectoryName(_kreNupkgPath));

            Console.WriteLine("Packing runtime {0}", Name);

            TargetPath = Path.Combine(root.PackagesPath, Name);

            if (Directory.Exists(TargetPath))
            {
                Console.WriteLine("  {0} already exists.", TargetPath);
                return;
            }

            if (!Directory.Exists(TargetPath))
            {
                Directory.CreateDirectory(TargetPath);
            }

            var targetNupkgPath = Path.Combine(TargetPath, Name + ".nupkg");
            using (var sourceStream = File.OpenRead(_kreNupkgPath))
            {
                using (var archive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                {
                    root.Operations.ExtractNupkg(archive, TargetPath);
                }
            }
            using (var sourceStream = File.OpenRead(_kreNupkgPath))
            {
                using (var targetStream = new FileStream(targetNupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sourceStream.CopyTo(targetStream);
                }

                sourceStream.Seek(0, SeekOrigin.Begin);
                var sha512Bytes = SHA512.Create().ComputeHash(sourceStream);
                File.WriteAllText(targetNupkgPath + ".sha512", Convert.ToBase64String(sha512Bytes));
            }
        }
    }
}