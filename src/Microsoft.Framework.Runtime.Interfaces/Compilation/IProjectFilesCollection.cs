using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Do not use this! It is going to go away soon!
    /// </summary>
    public interface IProjectFilesCollection
    {
        IEnumerable<string> PreprocessSourceFiles { get; }
        IEnumerable<string> ResourceFiles { get; }
        IEnumerable<string> SharedFiles { get; }
        IEnumerable<string> SourceFiles { get; }
        IEnumerable<string> GetFilesForBundling(bool includeSource, IEnumerable<string> additionalExcludePatterns);
    }
}
