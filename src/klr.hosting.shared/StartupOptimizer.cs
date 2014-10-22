using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace klr.hosting
{
    public class StartupOptimizer
    {
        private static readonly HashSet<string> s_excluded = new HashSet<string>()
        {
            "klr.host",
            "klr.core45.managed",
            "klr.net45.managed",
            "Microsoft.Framework.ApplicationHost",
            "Microsoft.Framework.Runtime",              //@@ Why ??? Expensive to JIT ???
            "Microsoft.Framework.Runtime.Roslyn",
            "Microsoft.AspNet.Loader.IIS",
            "Microsoft.AspNet.Hosting",
            "Microsoft.AspNet.PipelineCore",
            "Newtonsoft.Json"                           //@@ Expensive to JIT all methods (1000's)
        };

        private enum OptimizeMode
        {
            Off = 0,
            On = 1,
            Record = 2
        }

        private readonly OptimizeMode _optimizeMode;
        private readonly StartupProfiler _profiler = new StartupProfiler();
        private AssemblyPreloader _assemblyPreloader;

        public StartupOptimizer()
        {
            var profileModeNumString = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_MODE");
            int profileModeNum;
            if (!int.TryParse(profileModeNumString, out profileModeNum))
            {
                _optimizeMode = OptimizeMode.On;
            }
            else
            {
                _optimizeMode = (OptimizeMode)profileModeNum;
            }

            Trace.TraceInformation("[{0}] Optimize mode: ", GetType().Name, _optimizeMode);
        }

        /// <summary>
        /// Start needs to be called before loadFile is used
        /// </summary>
        public void Start(Func<string, Assembly> loadFile, Func<Stream, Assembly> loadStream, ConcurrentDictionary<string, Assembly> assemblyCache)
        {
            switch (_optimizeMode)
            {
                case OptimizeMode.On:
                    _assemblyPreloader = new AssemblyPreloader(loadFile, loadStream, assemblyCache);
                    var records = _profiler.GetAssemblyRecords();
                    if (records != null)
                    {
                        Task.Run(() => _assemblyPreloader.ScheduleProfilePlayback(records));
                    }
                    break;

                case OptimizeMode.Record:
                    _profiler.DispatchProfileWrite();
                    break;

                default:
                    break;
            }
        }

        public void RecordAssembly(Assembly assembly)
        {
            _profiler.RecordAssembly(assembly);
        }

        public void RecordAssemblyPath(string path)
        {
            _profiler.RecordAssembly(path);
        }

        public void ScheduleAssembly(Assembly assembly)
        {
            if (_assemblyPreloader != null)
            {
                _assemblyPreloader.ScheduleAssembly(assembly);
            }
            else
            {
                Trace.TraceInformation("[{0}] assembly preloader not initialized and {1} would not be preloaded", GetType().Name, assembly.GetName().Name);
            }
        }

        internal static bool ShouldExcludeAssembly(string assemblyName)
        {
            return s_excluded.Contains(assemblyName);
        }
    }
}