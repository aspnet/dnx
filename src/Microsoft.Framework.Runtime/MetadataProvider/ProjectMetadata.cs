using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.Runtime
{
    public class ProjectMetadata
    {
        public ProjectMetadata(Project project, ILibraryExport projectExport)
        {
            // Get the metadata reference for this project
            var projectReference = projectExport.MetadataReferences.OfType<IMetadataProjectReference>().First(r => string.Equals(r.Name, project.Name, StringComparison.OrdinalIgnoreCase));

            // Get all other metadata references
            var otherReferences = projectExport.MetadataReferences.Where(r => r != projectReference);

            SourceFiles = project.SourceFiles.ToList();

            RawReferences = otherReferences.OfType<IMetadataEmbeddedReference>().Select(r =>
            {
                return new
                {
                    Name = r.Name,
                    Bytes = r.Contents
                };
            })
            .ToDictionary(a => a.Name, a => a.Bytes);

            References = otherReferences.OfType<IMetadataFileReference>()
                                        .Select(r => r.Path)
                                        .ToList();

            ProjectReferences = otherReferences.OfType<IMetadataProjectReference>()
                                               .Select(r => r.ProjectPath)
                                               .ToList();

            var result = projectReference.GetDiagnostics();

            Errors = result.Errors.ToList();

            Warnings = result.Warnings.ToList();
        }

        public IList<string> SourceFiles
        {
            get;
            private set;
        }

        public IList<string> References
        {
            get;
            private set;
        }

        public IList<string> Errors
        {
            get;
            private set;
        }

        public IList<string> Warnings
        {
            get;
            private set;
        }

        public IDictionary<string, byte[]> RawReferences
        {
            get;
            private set;
        }

        public IList<string> ProjectReferences
        {
            get;
            private set;
        }
    }
}