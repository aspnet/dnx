using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

#if NET45
using System.Runtime.CompilerServices;
#endif

namespace klr.hosting
{
    public class AssemblyPreloader
    {
        //@@ Review if this concurrent tweak - help / hurt / no effect?
        private const int _concurrencyLevel = 4 * 64;
        private const int _initialCapacity = 64;
        private const int _prepareMethodsChunkSize = 2000;

        private readonly Func<string, Assembly> _loadFile;
        private readonly Func<Stream, Assembly> _loadStream;
        private readonly ConcurrentDictionary<string, Assembly> _assemblyCache;
        private readonly bool _enableAssemblyPrepare;
        private readonly string _assemblyPrepareFilesDirectory;

        private readonly ConcurrentDictionary<string, Assembly> _forceLoadAssemblyCache = new ConcurrentDictionary<string, Assembly>(_concurrencyLevel, _initialCapacity);
        private readonly ConcurrentDictionary<string, Assembly> _preJITMethodsAssembliesCache = new ConcurrentDictionary<string, Assembly>(_concurrencyLevel, _initialCapacity);

        public AssemblyPreloader(Func<string, Assembly> loadFile, Func<Stream, Assembly> loadStream, ConcurrentDictionary<string, Assembly> assemblyCache)
        {
            _loadFile = loadFile;
            _loadStream = loadStream;
            _assemblyCache = assemblyCache;

            _enableAssemblyPrepare = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PREPARE") == "1";
            _assemblyPrepareFilesDirectory = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PREPARE_FILES_DIRECTORY");

            if (string.IsNullOrEmpty(_assemblyPrepareFilesDirectory))
            {
                _assemblyPrepareFilesDirectory = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".kprep");
            }
        }

        public async Task ScheduleProfilePlayback(IEnumerable<string> records)
        {
            var delayValue = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PROFILE_PRELOAD_DELAY");
            int delay;
            if (!int.TryParse(delayValue, out delay))
            {
                delay = 100;
            }
            Trace.TraceInformation("[{0}] Delay playback, starting in {1} ms", GetType().Name, delay);
            await Task.Delay(delay);
            PreloadAssemblies(records);
        }

        public void ScheduleAssembly(Assembly assembly)
        {
            ScheduleAssembly(assembly, performWorkDirectlyOnCurrentThread: false);
        }

        private void PreloadAssemblies(IEnumerable<string> records)
        {
            Trace.TraceInformation("[{0}] PreloadAssemblies ENTERED", GetType().Name);

            var assembliesLoaded = new List<Assembly>();
            var lineCount = 0;
            foreach (var record in records)
            {
                if (string.IsNullOrEmpty(record))
                {
                    continue;
                }

                var assemblyPath = StartupProfiler.ResolveToAssemblyPath(record);
                var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                if (StartupOptimizer.ShouldExcludeAssembly(assemblyName))
                {
                    Trace.TraceInformation("[{0}] Excluding {1}.", GetType().Name, assemblyName);
                    continue;
                }

                var threadId = Thread.CurrentThread.ManagedThreadId;
                long startTime = DateTime.Now.Ticks;
                Trace.TraceInformation("[{0}] TID {1:00} ~~ SCHEDULED {2}", GetType().Name, threadId, assemblyPath);

                Assembly assembly;
                try
                {
                    assembly = _loadFile(assemblyPath);
                }
                catch (Exception e)
                {
                    Trace.TraceWarning("[{0}] TID {1:00} {2} EXCEPTION {3}", GetType().Name, threadId, assemblyPath, e.ToString());
                    continue;
                }

                if (assembly != null)
                {
                    assembliesLoaded.Add(assembly);
                    AddToAssemblyCache(assembly);
                    Trace.TraceInformation("[{0}] TID {1:00} LOADED || Elapsed {2}ms {3}", GetType().Name, threadId, (DateTime.Now.Ticks - startTime) / 10000, assemblyPath);
                }

                lineCount++;
            }

            if (_enableAssemblyPrepare)
            {
                foreach (var assembly in assembliesLoaded)
                {
                    ScheduleAssembly(assembly, true);
                }
            }

            Trace.TraceInformation("[{0}] PreloadAssemblies EXITING - Assemblies Loaded || {1}", GetType().Name, lineCount);
        }

        private void AddToAssemblyCache(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            _assemblyCache[name] = assembly;

            // Skip loading interfaces for dynamic assemblies
            if (assembly.IsDynamic)
            {
                return;
            }

            RuntimeBootstrapper.ExtractAssemblyNeutralInterfaces(assembly, _loadStream);
            Trace.TraceInformation(
                "[{0}] TID {1:00} {2} AddToAssemblyCache {3}",
                GetType().Name,
                Thread.CurrentThread.ManagedThreadId,
                DateTime.Now.ToString("HH:mm:ss.fff"),
                name);
        }


        // var types = Assembly.Load("FooAssembly, Version=1.2.3.4, Culture=neutral, PublicKeyToken=00000000000000").GetTypes()
        private void ScheduleAssembly(Assembly assembly, bool performWorkDirectlyOnCurrentThread)
        {
            Trace.TraceInformation("[{0}] ScheduleAssembly ENTER, name: {1}", GetType().Name, assembly.GetName().Name);

            var schedulePrepareAssembly = true;

            //@@TODO Check for .in.dll extension

            if (StartupOptimizer.ShouldExcludeAssembly(assembly.FullName))
            {
                Trace.TraceInformation("[{0}] Excluding {1}.", GetType().Name, assembly.FullName);
                schedulePrepareAssembly = false;
            }

            if (schedulePrepareAssembly)
            {
                Trace.TraceInformation("[{0}] ScheduleAssembly {1}", GetType().Name, assembly.GetName().Name);

                if (performWorkDirectlyOnCurrentThread)
                {
                    ForceLoadReferencedAssemblies(assembly);
#if ASPNETCORE50
                    //@@TODO PrepareMethod in CoreCLR ?
#else
                    PreJITMethods(assembly);
#endif
                }
                else
                {
                    Task.Factory.StartNew(_ =>
                    {
                        ForceLoadReferencedAssemblies(assembly);
                        PreJITMethods(assembly);
                    }
                    ,
                    null,
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskScheduler.Default);
                }
            }
        }

        // Recursively force load all of assemblies referenced by the given assembly
        private void ForceLoadReferencedAssemblies(Assembly assembly)
        {
            Trace.TraceInformation("ForceLoadReferencedAssemblies Enter {0}", assembly.FullName);
            ForceLoadReferencedAssembliesInternal(assembly);
            Trace.TraceInformation("ForceLoadReferencedAssemblies Exit {0}", assembly.FullName);
        }

        // Recursively load all of assemblies referenced by the given assembly
        private void ForceLoadReferencedAssembliesInternal(Assembly assembly)
        {
            string assemblyName = assembly.GetName().Name;

#if ASPNETCORE50
            //@@TODO
            //@@Skip KRE pathed assemblies?
#else
            //Skip assemblies from the GAC?
            if (assembly.GlobalAssemblyCache)
                return;
#endif

            // Skip dynamic assemblies?
            if (assembly.IsDynamic)
            {
                return;
            }

#if ASPNETCORE50
#else
            // why is this?
            //if (string.IsNullOrEmpty(LocationFromAssembly(assembly)))
            //{
            //    return;
            //}
#endif

            if (_forceLoadAssemblyCache.ContainsKey(assemblyName))
            {
                return;
            }

            if (_forceLoadAssemblyCache.TryAdd(assemblyName, assembly))
            {
                AddToAssemblyCache(assembly);

#if ASPNETCORE50
                //@@TODO var referencedAssemblies = assembly.GetReferencedAssemblies();
                //...
#else
                var referencedAssemblies = assembly.GetReferencedAssemblies();

                if (referencedAssemblies == null)
                {
                    return;
                }

                //Loop through ReferencedAssemblies
                foreach (var curAssemblyName in referencedAssemblies)
                {
                    Assembly nextAssembly = Assembly.Load(curAssemblyName);
                    ForceLoadReferencedAssembliesInternal(nextAssembly);
                }
#endif
            }
        }

        public void PreJITMethods(Assembly assembly)
        {
#if NET45
            // Skip dynamic assemblies? Revisit may be good for cold start latency ?
            if (assembly.IsDynamic)
            {
                Trace.TraceInformation("[{0}] PreJITMethods IsDynamic SKIP {1}", GetType().Name, assembly.GetName().Name);
                return;
            }

            //Check if already done PreJIT on this assembly
            if (_preJITMethodsAssembliesCache.ContainsKey(assembly.GetName().Name))
            {
                Trace.TraceInformation("[{0}] PreJITMethods ALREADY PreJIT {0}", GetType().Name, assembly.GetName().Name);
                return;
            }

            if (_preJITMethodsAssembliesCache.TryAdd(assembly.GetName().Name, assembly))
            {
                try
                {
                    List<MethodInfo> candidateMethods;
                    var prepareMethodFilepath = Path.Combine(_assemblyPrepareFilesDirectory, Path.GetFileNameWithoutExtension(assembly.FullName) + ".prepareMethods");
                    if (File.Exists(prepareMethodFilepath))
                    {
                        Trace.TraceInformation("[{0}] File.Exists TRUE $$$$$ {1} for {2}", GetType().Name, prepareMethodFilepath, assembly.GetName().Name);
                        candidateMethods = GetCandidateMethodsFromProfile(assembly, prepareMethodFilepath);
                    }
                    else
                    {
                        Trace.TraceInformation("[{0}] File.Exists False $$$$$ for {1}", GetType().Name, assembly.GetName().Name);
                        candidateMethods = GetCandidateMethodsFromAssembly(assembly);
                    }

                    // Schedule PrepareMethod chunks
                    Trace.TraceInformation("[{0}]  schedule PrepareMethod chunks - candidateMethods.Count {0} for {1}", GetType().Name, candidateMethods.Count, assembly.GetName().Name);

                    //if small method count schedule inline
                    if (candidateMethods.Count <= _prepareMethodsChunkSize)
                    {
                        // Run inline (on current thread)
                        PrepareMethods(assembly, candidateMethods, 0, candidateMethods.Count);
                    }
                    else
                    {
                        //Else - Chunk up large list and schedule on child Tasks
                        var lowerbound = 0;
                        var upperbound = _prepareMethodsChunkSize;
                        var prepareTasks = new List<Task>();
                        while(lowerbound < candidateMethods.Count)
                        {
                            var l = lowerbound;
                            var u = upperbound;
                            Action<object> prepareCallback = _ => PrepareMethods(assembly, candidateMethods, l, u);
                            var prepareTask = Task.Factory.StartNew(prepareCallback, null, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                            prepareTasks.Add(prepareTask);

                            lowerbound = upperbound;
                            upperbound += _prepareMethodsChunkSize;
                            if (upperbound > candidateMethods.Count)
                            {
                                upperbound = candidateMethods.Count;
                            }
                        }

                        //Wait for Tasks to complete
                        var swScheduleChunks = Stopwatch.StartNew();
                        Trace.TraceInformation("[{0}] Schedule chunks Task.WaitAll {1}", GetType().Name, assembly.GetName().Name);
                        Task.WaitAll(prepareTasks.ToArray());

                        swScheduleChunks.Stop();
                        Trace.TraceInformation("[{0}] Schedule chunks Task.WaitAll Elapsed {1:000}ms {2}", GetType().Name, swScheduleChunks.ElapsedMilliseconds, assembly.GetName().Name);
                    }
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    var sb = new StringBuilder();
                    foreach (var excep in rtle.LoaderExceptions)
                    {
                        sb.Append(excep.ToString());
                        sb.Append(@"\t\r\n");
                    }

                    Trace.TraceWarning("[{0}] PreJITMethods ReflectionTypeLoadException {0} {1} {2}", GetType(), rtle.ToString(), sb.ToString(), assembly.GetName().Name);
                }
            }
#endif
        }

#if NET45
        private List<MethodInfo> GetCandidateMethodsFromProfile(Assembly assembly, string prepareMethodFilepath)
        {
            var candidateMethods = new List<MethodInfo>();
            var prepareMethodLines = File.ReadLines(prepareMethodFilepath);
            Trace.TraceInformation("[{0}] File.ReadLines COUNT {1} for {2}", GetType().Name, prepareMethodLines.Count(), assembly.GetName().Name);
            var swMethodInfosFromPrepareFile = Stopwatch.StartNew();

            var lastTypeName = "";
            Type lastType = null;
            foreach (var prepareMethodLine in prepareMethodLines)
            {
                Type currentType = null;

                var lineParts = prepareMethodLine.Split(',');
                if ((lineParts == null) || (lineParts.Length < 2))
                    continue;

                var newTypeName = lineParts[0];
                var newMethodName = lineParts[1];

                // take advantage for when methods of the same type is grouped
                if ((lastType != null) && (string.Compare(lastTypeName, newTypeName) == 0))
                {
                    currentType = lastType;
                }
                else
                {
                    currentType = assembly.GetType(newTypeName);
                }

                if (currentType == null)
                {
                    Trace.TraceWarning("[{0}] type {1} cannot be resolved", GetType().Name, newTypeName);
                    continue;
                }

                lastType = currentType;
                lastTypeName = newTypeName;

                var currentMethods = currentType.GetTypeInfo().GetDeclaredMethods(newMethodName);
                if (currentMethods == null || currentMethods.Count() == 0)
                {
                    Trace.TraceWarning("[{0}] method {1} cannot be resolved", GetType().Name, newMethodName);
                    continue;
                }

                foreach (var currentMethod in currentMethods)
                {
                    // Abstract method need not be jitted
                    // TODO: add generic support
                    if (!currentMethod.IsAbstract && !currentMethod.ContainsGenericParameters)
                    {
                        candidateMethods.Add(currentMethod);
                    }
                }
            }

            swMethodInfosFromPrepareFile.Stop();
            Trace.TraceInformation("[{0}] Found ~~~~~ {1} MethodsInfos from {2} Elapsed {3:000}ms for {4}",
                GetType().Name,
                candidateMethods.Count,
                prepareMethodFilepath,
                swMethodInfosFromPrepareFile.ElapsedMilliseconds,
                assembly.GetName().Name);

            return candidateMethods;
        }

        private List<MethodInfo> GetCandidateMethodsFromAssembly(Assembly assembly)
        {
            var candidateMethods = new List<MethodInfo>();
            var types = assembly.GetTypes();

            Trace.TraceInformation("[{0}] TID {1:00} {2} PreJITMethods {3}",
                GetType().Name,
                Thread.CurrentThread.ManagedThreadId,
                DateTime.Now.ToString("HH:mm:ss.fff"),
                assembly.GetName().Name);

            var swPhase1 = Stopwatch.StartNew();

            foreach (var curType in types)
            {
                var methods = curType.GetMethods(
                        BindingFlags.DeclaredOnly |
                        BindingFlags.NonPublic |
                        BindingFlags.Public |
                        BindingFlags.Instance |
                        BindingFlags.Static);

                //Continue with loop if null method array
                if (methods == null)
                {
                    continue;
                }

                foreach (MethodInfo currentMethod in methods)
                {
                    // Abstract method need not be jitted
                    // TODO: add generic support
                    if (!currentMethod.IsAbstract && !currentMethod.ContainsGenericParameters)
                    {
                        candidateMethods.Add(currentMethod);
                    }
                }
            }

            return candidateMethods;
        }

        private void PrepareMethods(Assembly assembly, List<MethodInfo> candidateMethods, int startIndex, int endIndex)
        {
            var swMethodInfos = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}] PrepareMethods {1} range: [{2}, {3})", GetType().Name, assembly.GetName().Name, startIndex, endIndex);

            for (int i = startIndex; i < endIndex; i++)
            {
                RuntimeHelpers.PrepareMethod(candidateMethods[i].MethodHandle);
            }

            swMethodInfos.Stop();
            Trace.TraceInformation("[{0}] TID {1:00} {2} PrepareMethods {3} %%%%% [{4}, {5}) Elapsed {6:000}ms",
                GetType().Name,
                Thread.CurrentThread.ManagedThreadId,
                assembly.GetName().Name,
                DateTime.Now.ToString("HH:mm:ss.fff"),
                startIndex,
                endIndex,
                swMethodInfos.ElapsedMilliseconds);
        }
#endif
    }
}