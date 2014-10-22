using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace klr.hosting
{
    public class StartupProfiler
    {
        private readonly string _profileDirectory;
        private Writer _profileWriter;

        private string ProfileFileName
        {
            get { return Path.Combine(_profileDirectory, "project-assemblies.ini"); }
        }

        public StartupProfiler()
        {
#if ASPNETCORE50
            string applicationBaseDirectory = AppContext.BaseDirectory;
#else
            // REVIEW: Need a way to set the application base on mono
            //@@PlatformHelper.IsMono ?
            //@@Directory.GetCurrentDirectory() :
            string applicationBaseDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
#endif
            var applicationBinDirectory = Path.Combine(applicationBaseDirectory, "bin");

            //@@TODO add support for Azure Websites "data" directory

            _profileDirectory = Path.Combine(
                applicationBinDirectory,
                ".kstartup",
#if ASPNETCORE50
                "ASPNETCORE50",
#else
                "NET45",
#endif
                Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"));

            if (!Directory.Exists(_profileDirectory))
            {
                Directory.CreateDirectory(_profileDirectory);
            }
        }

        public IEnumerable<string> GetAssemblyRecords()
        {
            var profileFile = ProfileFileName;
            if (!File.Exists(profileFile))
            {
                Trace.TraceError("[{0}] Profile not found in location {1}", GetType().Name, profileFile);
                return null;
            }

            //@@string projectJsonFilename = "project.json";
            //@@TODO make sure KRE directory version is unchanged - compute simple HASH on directoryname
            //@@TODO make sure project.json is unchanged - compute HASH on content
            //@@TODO make sure global.json is unchanged - compute HASH on content

            var lines = File.ReadLines(profileFile);

            Trace.TraceInformation("[{0}] File.ReadLines {1}", GetType().Name, lines.ToString());

            return lines;
        }

        public void DispatchProfileWrite()
        {
            _profileWriter = new Writer(this);
            Task.Run(() => _profileWriter.StartProfileWrite());
        }

        public void RecordAssembly(Assembly assembly)
        {
            if (_profileWriter == null)
            {
                return;
            }

            _profileWriter.Record(assembly);
        }

        public void RecordAssembly(string path)
        {
            if (_profileWriter == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("Null path not allowed " + path);
            }

            _profileWriter.Record(path);
        }

        private class Writer
        {
            private readonly ConcurrentQueue<string> _assemblyPathRecordQueue = new ConcurrentQueue<string>();
            private readonly StartupProfiler _parent;

            public Writer(StartupProfiler parent)
            {
                _parent = parent;
            }

            public async Task StartProfileWrite()
            {
                var writeDelayVal = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PROFILE_WRITE_DELAY");
                int writeDelay;
                if (!int.TryParse(writeDelayVal, out writeDelay))
                {
                    writeDelay = 20000;  // 20 seconds by default
                }

                await Task.Delay(writeDelay);

                var outputFile = _parent.ProfileFileName;
                Trace.TraceInformation("[{0}] StartProfileWrite Queue Count: {1}, output file: {2}", GetType().Name, _assemblyPathRecordQueue.Count, outputFile);

                var lineCount = 0;
                var written = new HashSet<string>();

                using (var assemblyProfileWriter = new StreamWriter(new FileStream(outputFile, FileMode.Create)))
                {
                    string line;
                    while (_assemblyPathRecordQueue.TryDequeue(out line))
                    {
                        //Check if already written

                        //Dedup
                        if (written.Contains(line))
                        {
                            continue;
                        }

                        await assemblyProfileWriter.WriteLineAsync(line);
                        written.Add(line);
                        lineCount++;
                    }
                }

                Trace.TraceInformation("[{0}] StartProfileWrite Write LineCount: {1}", GetType().Name, lineCount);
            }

            public void Record(Assembly assembly)
            {
                var recordLine = StartupProfiler.GetAssemblyLocation(assembly);
                if (!string.IsNullOrEmpty(recordLine))
                {
                    _assemblyPathRecordQueue.Enqueue(recordLine);
                }
            }
            public void Record(string path)
            {
                _assemblyPathRecordQueue.Enqueue(path);
            }
        }

        public static string ResolveToAssemblyPath(string record)
        {
            // We always record the path
            return record;
        }

        public static string GetAssemblyLocation(Assembly assembly)
        {
#if ASPNETCORE50
            //@@TODO return assembly.Location;
            //@@ Check if RuntimeAssembly ?
            //@@ if so, use private reflection to extract the path
            return null;
#else
                return assembly.Location;
#endif
        }
    }
}