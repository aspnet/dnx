using System;

namespace Microsoft.Framework.Runtime
{
    public interface IProjectReader
    {
        bool TryReadProject(string projectName, out Project project);
    }
}