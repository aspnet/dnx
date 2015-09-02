using System.IO;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Testing
{
    public class Dnx
    {
        private readonly string _sdkPath;

        public Dnx(string sdkPath)
        {
            _sdkPath = sdkPath;
        }

        public ExecResult Execute(string commandLine, bool dnxTraceOn = true)
        {
            var dnxPath = Path.Combine(_sdkPath, "bin", "dnx");
            return Exec.Run(
                dnxPath,
                commandLine,
                env => env[EnvironmentNames.Trace] = dnxTraceOn ? "1" : null);
        }
    }
}
