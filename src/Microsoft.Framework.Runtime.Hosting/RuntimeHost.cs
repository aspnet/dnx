using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class RuntimeHost
    {
        public Project Project { get; }
        public string ApplicationBaseDirectory { get; }
        public NuGetFramework TargetFramework { get; }
        public IAssemblyLoaderContainer LoaderContainer { get; }
        public IEnumerable<IDependencyProvider> DependencyProviders { get; }

        internal RuntimeHost(RuntimeHostBuilder builder, Project project)
        {
            Project = project;

            // Load properties from the mutable RuntimeHostBuilder into
            // immutable copies on this object
            ApplicationBaseDirectory = builder.ApplicationBaseDirectory;
            LoaderContainer = builder.LoaderContainer;
            TargetFramework = builder.TargetFramework;

            // Copy the dependency providers so the user can't fiddle with them without our knowledge
            var list = new List<IDependencyProvider>(builder.DependencyProviders);
            DependencyProviders = list;
        }

        public void LaunchApplication(string applicationName, string[] programArgs)
        {
            Logger.TraceInformation($"Launching '{applicationName}' '{string.Join(" ", programArgs)}'");

            // Walk dependencies
            var walker = new DependencyWalker(DependencyProviders);
            var result = walker.Walk(Project.Name, Project.Version, TargetFramework);

            // Write the resolved graph
            if(Logger.IsEnabled)
            {
                Console.WriteLine("Dependency Graph:");
                result.Dump(s => Console.WriteLine(s));
            } 

            // Locate the entry point
            var entryPoint = LocateEntryPoint(applicationName);

            Logger.TraceInformation($"Executing Entry Point: {entryPoint.GetName().FullName}");
        }

        private Assembly LocateEntryPoint(string applicationName)
        {
            var sw = Stopwatch.StartNew();
            Logger.TraceInformation($"Locating entry point for {applicationName}");

            if (Project == null)
            {
                Logger.TraceError("[RuntimeHost] Unable to locate entry point, there is no project");
                return null;
            }

            Assembly asm = null;
            try
            {
                asm = Assembly.Load(new AssemblyName(applicationName));
            }
            catch (FileLoadException ex) when (string.Equals(new AssemblyName(ex.FileName).Name, applicationName, StringComparison.Ordinal))
            {
                if(ex.InnerException is ICompilationException)
                {
                    throw ex.InnerException;
                }

                ThrowEntryPointNotFoundException(applicationName, ex);
            }
            catch(FileNotFoundException ex) when (string.Equals(ex.FileName, applicationName, StringComparison.Ordinal))
            {
                if(ex.InnerException is ICompilationException)
                {
                    throw ex.InnerException;
                }

                ThrowEntryPointNotFoundException(applicationName, ex);
            }

            sw.Stop();
            Logger.TraceInformation($"Located entry point in {sw.ElapsedMilliseconds}ms");

            return asm;
        }

        private void ThrowEntryPointNotFoundException(
            string applicationName,
            Exception innerException)
        {
            if (Project.Commands.Any())
            {
                // Throw a nicer exception message if the command
                // can't be found
                throw new InvalidOperationException(
                    string.Format("Unable to load application or execute command '{0}'. Available commands: {1}.",
                    applicationName,
                    string.Join(", ", Project.Commands.Keys)), innerException);
            }

            throw new InvalidOperationException(
                    string.Format("Unable to load application or execute command '{0}'.",
                    applicationName), innerException);
        }
    }
}