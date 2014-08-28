// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if ASPNETCORE50
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace klr.hosting
{
    public class DelegateAssemblyLoadContext : AssemblyLoadContext
    {
        private Func<AssemblyName, Assembly> _loaderCallback;

        public DelegateAssemblyLoadContext(Func<AssemblyName, Assembly> loaderCallback)
        {
            _loaderCallback = loaderCallback;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return _loaderCallback(assemblyName);
        }

        public Assembly LoadFile(string path)
        {
            // Look for platform specific native image
            string nativeImagePath = GetNativeImagePath(path);

            if (nativeImagePath != null)
            {
                var assemblyNI = LoadFromNativeImagePath(nativeImagePath, path);
                if (assemblyNI != null)
                {
                    StartupOptimizer.AssemblyProfile_RecordQueueAdd(nativeImagePath);
                }
                return assemblyNI;
            }

            var assembly = LoadFromAssemblyPath(path);
            if (assembly != null)
            {
                StartupOptimizer.AssemblyProfile_RecordQueueAdd(path);
            }
            return assembly;
        }

        public Assembly LoadStream(Stream assemblyStream, Stream pdbStream)
        {
            if (pdbStream == null)
            {
                return LoadFromStream(assemblyStream);
            }

            return LoadFromStream(assemblyStream, pdbStream);
        }

        private string GetNativeImagePath(string ilPath)
        {
            var directory = Path.GetDirectoryName(ilPath);
            var arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");

            var nativeImageName = Path.GetFileNameWithoutExtension(ilPath) + ".ni.dll";
            var nativePath = Path.Combine(directory, arch, nativeImageName);

            if (File.Exists(nativePath))
            {
                return nativePath;
            }
            else
            {
                // KRE is arch sensitive so the ni is in the same folder as IL
                nativePath = Path.Combine(directory, nativeImageName);
                if (File.Exists(nativePath))
                {
                    return nativePath;
                }
            }

            return null;
        }
        
        //CoreCLR - MultiCoreJit
        public bool EnableMultiCoreJit(string profileOptimizationRootPath)
        {
            SetProfileOptimizationRoot(profileOptimizationRootPath);

            return true;
        }

        public void StartMultiCoreJitProfile(string profileFilename)
        {
            StartProfileOptimization(profileFilename);
        }
    }
}
#endif