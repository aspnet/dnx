using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation
{
    public class ProjectExport : LibraryExport, IDisposable
    {
        private ApplicationHostContext _appHost;
        private LibraryExport _dependenciesExport;

        private Project _project;
        private FrameworkName _targetFramework;
        private LibraryExporter _exporter;

        public ProjectExport(LibraryExporter exporter, Project project, FrameworkName targetFramework, IList<IMetadataReference> metadataReferences, IList<ISourceReference> sourceReferences)
            : base(metadataReferences, sourceReferences)
        {
            _project = project;
            _targetFramework = targetFramework;
            _exporter = exporter;
        }

        public LibraryExport DependenciesExport
        {
            get
            {
                if (_dependenciesExport == null)
                {
                    var mainProject = ApplicationHostContext.MainProject;

                    _dependenciesExport = _exporter.GetAllExports(mainProject, aspect: null, include: ld => ld != mainProject);

                    _exporter = null;
                }

                return _dependenciesExport;
            }
        }

        public ApplicationHostContext ApplicationHostContext
        {
            get
            {
                if (_appHost == null)
                {
                    // This library manager represents the graph that will be used to resolve
                    // references (compiler /r in csc terms)
                    var context = new ApplicationHostContext
                    {
                        Project = _project,
                        TargetFramework = _targetFramework,
                        SkipLockfileValidation = true
                    };

                    ApplicationHostContext.Initialize(context);
                    _appHost = context;

                    _project = null;
                    _targetFramework = null;

                }
                return _appHost;
            }
        }

        public IMetadataProjectReference ProjectReference
        {
            get
            {
                return MetadataReferences.Count == 0 ? null : MetadataReferences[0] as IMetadataProjectReference;
            }
        }

        public IAssemblyLoadContext LoadContext { get; set; }

        public void Dispose()
        {
            LoadContext?.Dispose();
        }
    }
}
