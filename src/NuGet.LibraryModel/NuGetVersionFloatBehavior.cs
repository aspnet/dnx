using System;

namespace NuGet.Versioning
{
    public enum NuGetVersionFloatBehavior
    {
        None,
        Prerelease,
        Revision,
        Build,
        Minor,
        Major
    }

}