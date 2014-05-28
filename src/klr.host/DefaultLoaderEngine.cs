// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Framework.Runtime;

namespace klr.host
{
    public class DefaultLoaderEngine : IAssemblyLoaderEngine
    {
        private readonly Func<string, Assembly> _loadFile;
        private readonly Func<Stream, Stream, Assembly> _loadStream;

        public DefaultLoaderEngine(object loaderImpl)
        {
            if (loaderImpl == null)
            {
                throw new ArgumentNullException("loaderImpl");
            }

            var typeInfo = loaderImpl.GetType().GetTypeInfo();
            var loaderFileMethod = typeInfo.GetDeclaredMethod("LoadFile");
            var loadStreamMethod = typeInfo.GetDeclaredMethod("LoadStream");

            _loadFile = (Func<string, Assembly>)loaderFileMethod.CreateDelegate(typeof(Func<string, Assembly>), loaderImpl);
            _loadStream = (Func<Stream, Stream, Assembly>)loadStreamMethod.CreateDelegate(typeof(Func<Stream, Stream, Assembly>), loaderImpl);
        }

        public Assembly LoadFile(string path)
        {
            return _loadFile(path);
        }

        public Assembly LoadStream(Stream assemblyStream, Stream pdbStream)
        {
            return _loadStream(assemblyStream, pdbStream);
        }
    }
}
