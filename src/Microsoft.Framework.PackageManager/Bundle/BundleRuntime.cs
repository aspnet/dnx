// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Microsoft.Framework.PackageManager.Bundle
{
    public class BundleRuntime
    {
        private readonly FrameworkName _frameworkName;
        private readonly string _runtimeNupkgPath;

        public BundleRuntime(BundleRoot root, FrameworkName frameworkName, string runtimeNupkgPath)
        {
            _frameworkName = frameworkName;
            _runtimeNupkgPath = runtimeNupkgPath;
            Name = Path.GetFileName(Path.GetDirectoryName(_runtimeNupkgPath));
            TargetPath = Path.Combine(root.TargetPackagesPath, Name);
        }

        public string Name { get; private set; }
        public string TargetPath { get; private set; }
        public FrameworkName Framework { get { return _frameworkName; } }

        public void Emit(BundleRoot root)
        {
            root.Reports.Quiet.WriteLine("Bundling runtime {0}", Name);

            if (Directory.Exists(TargetPath))
            {
                root.Reports.Quiet.WriteLine("  {0} already exists.", TargetPath);
                return;
            }

            if (!Directory.Exists(TargetPath))
            {
                Directory.CreateDirectory(TargetPath);
            }

            var targetNupkgPath = Path.Combine(TargetPath, Name + ".nupkg");
            using (var sourceStream = File.OpenRead(_runtimeNupkgPath))
            {
                using (var archive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                {
                    root.Operations.ExtractNupkg(archive, TargetPath);
                }
            }
            using (var sourceStream = File.OpenRead(_runtimeNupkgPath))
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