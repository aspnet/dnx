// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Versioning;

namespace NuGet
{
    internal class ZipPackageFile : IPackageFile
    {
        private readonly Func<Stream> _streamFactory;
        private readonly FrameworkName _targetFramework;

        public ZipPackageFile(ZipArchiveEntry part)
            : this(part.FullName.Replace('/', '\\'), part.Open().ToStreamFactory())
        {
        }

        public ZipPackageFile(IPackageFile file)
            : this(file.Path, file.GetStream().ToStreamFactory())
        {
        }

        protected ZipPackageFile(string path, Func<Stream> streamFactory)
        {
            Path = path;
            _streamFactory = streamFactory;

            string effectivePath;
            _targetFramework = VersionUtility.ParseFrameworkNameFromFilePath(path, out effectivePath);
            EffectivePath = effectivePath;
        }

        public string Path
        {
            get;
            private set;
        }

        public string EffectivePath
        {
            get;
            private set;
        }

        public FrameworkName TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        IEnumerable<FrameworkName> IFrameworkTargetable.SupportedFrameworks
        {
            get
            {
                if (TargetFramework != null)
                {
                    yield return TargetFramework;
                }
                yield break;
            }
        }

        public Stream GetStream()
        {
            return _streamFactory();
        }

        public override string ToString()
        {
            return Path;
        }
    }
}