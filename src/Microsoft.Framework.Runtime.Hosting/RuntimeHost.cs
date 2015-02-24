using System;
using System.Diagnostics;
using System.Reflection;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Hosting
{
    public class RuntimeHost
    {
        public Project Project { get; }
        public string ApplicationBaseDirectory { get; }
        public IAssemblyLoaderContainer LoaderContainer { get; }

        internal RuntimeHost(RuntimeHostBuilder builder, Project project)
        {
            Project = project;

            // Load properties from the mutable RuntimeHostBuilder into
            // immutable copies on this object
            ApplicationBaseDirectory = builder.ApplicationBaseDirectory;
            LoaderContainer = builder.LoaderContainer;
        }

        public void LaunchApplication(string applicationName, string[] programArgs)
        {
            Logger.TraceInformation($"Launching '{applicationName}' '{string.Join(" ", programArgs)}'");

            // Locate the entry point
            var entryPoint = LocateEntryPoint(applicationName);
        }

        private Assembly LocateEntryPoint(string applicationName)
        {
            var sw = Stopwatch.StartNew();
        }
    }
}