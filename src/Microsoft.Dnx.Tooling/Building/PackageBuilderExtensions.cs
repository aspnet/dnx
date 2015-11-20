using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet;

namespace Microsoft.Dnx.Tooling.Building
{
    public static class PackageBuilderExtensions
    {
        public static void InitializeFromProject(this PackageBuilder builder, Runtime.Project project)
        {
            builder.Authors.AddRange(project.Authors);
            builder.Owners.AddRange(project.Owners);

            if (builder.Authors.Count == 0)
            {
                // TODO: DNX_AUTHOR is a temporary name
                var defaultAuthor = Environment.GetEnvironmentVariable("DNX_AUTHOR");
                if (string.IsNullOrEmpty(defaultAuthor))
                {
                    builder.Authors.Add(project.Name);
                }
                else
                {
                    builder.Authors.Add(defaultAuthor);
                }
            }

            builder.Description = project.Description ?? project.Name;
            builder.Id = project.Name;
            builder.Version = project.Version;
            builder.Title = project.Title;
            builder.Summary = project.Summary;
            builder.Copyright = project.Copyright;
            builder.RequireLicenseAcceptance = project.RequireLicenseAcceptance;
            builder.ReleaseNotes = project.ReleaseNotes;
            builder.Language = project.Language;
            builder.Tags.AddRange(project.Tags);

            if (!string.IsNullOrEmpty(project.IconUrl))
            {
                builder.IconUrl = new Uri(project.IconUrl);
            }

            if (!string.IsNullOrEmpty(project.ProjectUrl))
            {
                builder.ProjectUrl = new Uri(project.ProjectUrl);
            }

            if (!string.IsNullOrEmpty(project.LicenseUrl))
            {
                builder.LicenseUrl = new Uri(project.LicenseUrl);
            }
        }
    }
}
