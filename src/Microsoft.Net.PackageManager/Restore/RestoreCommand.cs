using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Loader.NuGet;
using NuGet;
using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Microsoft.Net.PackageManager
{
    public class WalkProviderMatch
    {
        public IWalkProvider Provider { get; set; }
        public Library Library { get; set; }
        public string Path { get; set; }
    }

    public class WalkProviderDependencies
    {
        public IWalkProvider Provider { get; set; }
        public Library Library { get; set; }
        public string Path { get; set; }
    }

    public interface IWalkProvider
    {
        Task<WalkProviderMatch> FindLibraryByName(string name, FrameworkName targetFramework);
        Task<WalkProviderMatch> FindLibraryByVersion(Library library, FrameworkName targetFramework);
        Task<WalkProviderMatch> FindLibraryBySnapshot(Library library, FrameworkName targetFramework);
        Task<IEnumerable<Library>> GetDependencies(Library library, FrameworkName targetFramework);
    }

    public class WalkProvider : IWalkProvider
    {
        IDependencyProvider _dependencyProvider;

        public WalkProvider(IDependencyProvider dependencyProvider)
        {
            _dependencyProvider = dependencyProvider;
        }

        public async Task<WalkProviderMatch> FindLibraryByName(string name, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(name, new SemanticVersion(new Version(0, 0)), targetFramework);
            if (description == null)
            {
                return null;
            }
            return new WalkProviderMatch
            {
                Library = description.Identity,
                Path = description.Path,
                Provider = this,
            };
        }

        public async Task<WalkProviderMatch> FindLibraryByVersion(Library library, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(library.Name, library.Version, targetFramework);
            if (description == null)
            {
                return null;
            }
            return new WalkProviderMatch
            {
                Library = description.Identity,
                Path = description.Path,
                Provider = this,
            };
        }

        public async Task<WalkProviderMatch> FindLibraryBySnapshot(Library library, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(library.Name, library.Version, targetFramework);
            if (description == null)
            {
                return null;
            }
            return new WalkProviderMatch
            {
                Library = description.Identity,
                Path = description.Path,
                Provider = this,
            };
        }

        public async Task<IEnumerable<Library>> GetDependencies(Library library, FrameworkName targetFramework)
        {
            var description = _dependencyProvider.GetDescription(library.Name, library.Version, targetFramework);
            return description.Dependencies;
        }
    }

    public class RestoreContext
    {
        public RestoreContext()
        {

        }

        public TargetFrameworkConfiguration TargetFrameworkConfiguration { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }
    }

    public class RestoreCommand
    {
        public RestoreCommand()
        {
            FileSystem = new PhysicalFileSystem(Environment.CurrentDirectory);
            MachineWideSettings = new CommandLineMachineWideSettings();
        }

        public string RestoreDirectory { get; set; }
        public string ConfigFile { get; set; }
        public IMachineWideSettings MachineWideSettings { get; set; }
        public IFileSystem FileSystem { get; set; }
        protected internal ISettings Settings { get; set; }
        protected internal IPackageSourceProvider SourceProvider { get; set; }

        public void ExecuteCommand()
        {
            var restoreDirectory = RestoreDirectory ?? Directory.GetCurrentDirectory();

            var projectJsonFiles = Directory.GetFiles(restoreDirectory, "project.json", SearchOption.AllDirectories);

            var rootDirectory = ResolveRootDirectory(restoreDirectory);

            ReadSettings(rootDirectory);

            string packagesFolder = Path.Combine(rootDirectory, CommandLineConstants.PackagesDirectoryName);
            var packagesFolderFileSystem = CreateFileSystem(packagesFolder);
            var pathResolver = new DefaultPackagePathResolver(packagesFolderFileSystem, useSideBySidePaths: true);
            var localRepository = new LocalPackageRepository(pathResolver, packagesFolderFileSystem)
            {
                //PackageSaveMode = PackageSaveModes.Nuspec | PackageSaveModes.Nupkg,
            };

            foreach (var projectJsonPath in projectJsonFiles)
            {
                RestoreForProject(localRepository, projectJsonPath, rootDirectory);
            }
        }

        private void RestoreForProject(LocalPackageRepository localRepository, string projectJsonPath, string rootDirectory)
        {
            Project project;
            if (!Project.TryGetProject(projectJsonPath, out project))
            {
                throw new Exception("TODO: project.json parse error");
            }

            var restoreOperations = new RestoreOperations();
            var projectProviders = new List<IWalkProvider>();
            var localProviders = new List<IWalkProvider>();
            var remoteProviders = new List<IWalkProvider>();
            var contexts = new List<RestoreContext>();

            projectProviders.Add(new WalkProvider(new ProjectReferenceDependencyProvider(
                new ProjectResolver(project.ProjectDirectory, rootDirectory))));

            localProviders.Add(new WalkProvider(new NuGetDependencyResolver(
                Path.GetDirectoryName(projectJsonPath), 
                Path.Combine(rootDirectory, CommandLineConstants.PackagesDirectoryName))));

            foreach (var configuration in project.GetTargetFrameworkConfigurations())
            {
                var context = new RestoreContext
                {
                    TargetFrameworkConfiguration = configuration,
                    ProjectLibraryProviders = projectProviders,
                    LocalLibraryProviders = localProviders,
                    RemoteLibraryProviders = remoteProviders,
                };
                contexts.Add(context);
            }

            var tasks = new List<Task>();
            foreach (var context in contexts)
            {
                tasks.Add(restoreOperations.CreateGraph(context, new Library { Name = project.Name, Version = project.Version }));
            }
            Task.WhenAll(tasks).Wait();

#if false
            Project project;
            if (Project.TryGetProject(projectJsonPath, out project))
            {
                var projectResolver = new ProjectResolver(project.ProjectDirectory, rootDirectory);
                var projectDependencyProvider = new ProjectReferenceDependencyProvider(projectResolver);
                var nugetDependencyProvider = new NuGetDependencyProvider(localRepository, CreateRepository());

                foreach (var configuration in project.GetTargetFrameworkConfigurations())
                {
                    var walker = new DependencyWalker(new IDependencyProvider[] {
                        projectDependencyProvider,
                        nugetDependencyProvider
                    });

                    walker.Walk(project.Name, project.Version, configuration.FrameworkName);
                    System.Console.WriteLine();

                    Parallel.ForEach(nugetDependencyProvider.Dependencies, d =>
                    {
                        string packageDirectory = localRepository.PathResolver.GetPackageDirectory(d.Identity.Package);

                        // Add files
                        localRepository.FileSystem.AddFiles(d.Identity.Package.GetFiles(), packageDirectory);

                        localRepository.AddPackage(d.Identity.Package);
                    });
                }
            }
#endif
        }

        public static string ResolveRootDirectory(string projectDir)
        {
            var di = new DirectoryInfo(projectDir);

            while (di.Parent != null)
            {
                if (di.EnumerateFiles("*.global.json").Any() ||
                    di.EnumerateFiles("*.sln").Any() ||
                    di.EnumerateDirectories("packages").Any() ||
                    di.EnumerateDirectories(".git").Any())
                {
                    return di.FullName;
                }

                di = di.Parent;
            }

            return Path.GetDirectoryName(projectDir);
        }


        private void ReadSettings(string solutionDirectory)
        {
            // Read the solution-level settings
            var solutionSettingsFile = Path.Combine(
                solutionDirectory,
                NuGetConstants.NuGetSolutionSettingsFolder);
            var fileSystem = CreateFileSystem(solutionSettingsFile);

            if (ConfigFile != null)
            {
                ConfigFile = FileSystem.GetFullPath(ConfigFile);
            }

            Settings = NuGet.Settings.LoadDefaultSettings(
                fileSystem: fileSystem,
                configFileName: ConfigFile,
                machineWideSettings: MachineWideSettings);

            // Recreate the source provider and credential provider
            SourceProvider = PackageSourceBuilder.CreateSourceProvider(Settings);
            //HttpClient.DefaultCredentialProvider = new SettingsCredentialProvider(new ConsoleCredentialProvider(Console), SourceProvider, Console);

        }

        private IFileSystem CreateFileSystem(string path)
        {
            path = FileSystem.GetFullPath(path);
            return new PhysicalFileSystem(path);
        }

    }
}