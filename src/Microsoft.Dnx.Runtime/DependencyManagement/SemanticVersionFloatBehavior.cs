using System;

namespace Microsoft.Dnx.Runtime
{
    public enum SemanticVersionFloatBehavior
    {
        None,
        Prerelease,
        Revision,
        Build,
        Minor,
        Major
    }

}