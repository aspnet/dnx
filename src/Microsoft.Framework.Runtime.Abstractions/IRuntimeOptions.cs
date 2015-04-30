using System;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Represents the options passed into the runtime on boot
    /// </summary>
    // REVIEW: Should this be an interface or just a poco?
    // REVIEW: Do we need to repeat the things that appear on IApplicationEnvironment?
    public interface IRuntimeOptions
    {
        string ApplicationName { get; }

        string ApplicationBaseDirectory { get; }

        string PackageDirectory { get; }

        FrameworkName TargetFramework { get; }

        string Configuration { get; }

        bool WatchFiles { get; }

        int? CompilationServerPort { get; }
    }
}