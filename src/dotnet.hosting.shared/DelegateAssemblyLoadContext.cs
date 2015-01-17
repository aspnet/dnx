// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if ASPNETCORE50
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace dotnet.hosting
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
                return LoadFromNativeImagePath(nativeImagePath, path);
            }

            return LoadFromAssemblyPath(path);
        }

        public Assembly LoadStream(Stream assemblyStream, Stream assemblySymbols)
        {
            if (assemblySymbols == null)
            {
                return LoadFromStream(assemblyStream);
            }

            return LoadFromStream(assemblyStream, assemblySymbols);
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
                // Runtime is arch sensitive so the ni is in the same folder as IL
                nativePath = Path.Combine(directory, nativeImageName);
                if (File.Exists(nativePath))
                {
                    return nativePath;
                }
            }

            return null;
        }
        
        public bool EnableMultiCoreJit()
        {
            var appBaseDirectory = AppContext.BaseDirectory;
            var appBinDirectoryName = "bin";
            var appBinDirectory = Path.Combine(appBaseDirectory, appBinDirectoryName);
            var appProfileDirectoryName = "profile";
            var appProfileDirectory = Path.Combine(appBinDirectory, appProfileDirectoryName);
            
            if (!Directory.Exists(appProfileDirectory))
            {
                Directory.CreateDirectory(appProfileDirectory);
                
                try 
                {
                    Directory.CreateDirectory(appProfileDirectory);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            
            SetProfileOptimizationRoot(appProfileDirectory);

            return true;
        }
        
        public void StartMultiCoreJitProfile(string profileFilename)
        {
            StartProfileOptimization(profileFilename);
        }
    }
}
#endif