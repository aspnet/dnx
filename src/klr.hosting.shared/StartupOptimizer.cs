// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace klr.hosting
{
    internal static class StartupOptimizer
    {
/*
        Experimental
        
        Provides the following capabilities:

        1) AssemblyProfile - Records list of assemblies used in startup
            Dynamically adds assembly filepaths to a queue (using loading hooks in RuntimeBootstrapper/DelegateAssemblyLoadContext.cs) 
            After a delay to let app start (~15 seconds) a timer fires which dedupes the queue entries 
            and writes to startup profile file file on disk

        2) AssemblyProfile - Playbacks (preloads) list of assemblies used in startup
            Checks if project.json is unchanged
            PreLoads assemblies from startup profile file

        3) AssemblyPrepare - Prepares methods from assemblies that are used in startup
            Call .PrepareMethod (forces IL method to be JITTED) on all methods from all types in assembly.
            Chunks list to to seperate Tasks if large number of methods (5000+)
            
        4) MultiCoreJit - Enable CLR based MultiCore JIT on CoreCLR and NETFX
        
        Environment Variables
        
        SET KRE_TRACE=1
        SET KRE_OPTIMIZER_ASSEMBLY_PROFILE=1
        SET KRE_OPTIMIZER_ASSEMBLY_PREPARE=1
        SET KRE_OPTIMIZER_ASSEMBLY_PREPARE_FILES_DIRECTORY=<path>
        SET KRE_OPTIMIZER_ASSEMBLY_PREPARE_FILES_DIRECTORY=c:\users\{username}\.kprep
*/
    
        static bool _enableTrace = false;
        static bool _enableAssemblyProfileRecordPlayback = false; // = true;
        static bool _enableAssemblyPrepare = false; // = true;
        static string _assemblyPrepareFilesDirectory = "";
        
        //Reference to original RuntimeBootstrapper._assemblyCache
        //Used to patch RuntimeBootstrapper._assemblyCache when Preloading assemblies
        private static ConcurrentDictionary<string, Assembly> _assemblyCache = null;

#if ASPNETCORE50
        private static DelegateAssemblyLoadContext _loaderImpl = null;
#else
        private static LoaderEngine _loaderImpl = null;
#endif

        private static Func<string, Assembly> _loadFile = null;
        private static Func<Stream, Assembly> _loadStream = null;

        public static void Init(
                                    ConcurrentDictionary<string, Assembly> assemblyCache,
#if ASPNETCORE50
                                    DelegateAssemblyLoadContext loaderImpl,
#else
                                    LoaderEngine loaderImpl,
#endif
                                    Func<string, Assembly> loadFile,
                                    Func<Stream, Assembly> loadStream
                                )
        {
            _enableTrace = Environment.GetEnvironmentVariable("KRE_TRACE") == "1";
            _enableAssemblyProfileRecordPlayback = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PROFILE") == "1";
            _enableAssemblyPrepare = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PREPARE") == "1";

            _assemblyPrepareFilesDirectory = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PREPARE_FILES_DIRECTORY");
            
            if (String.IsNullOrEmpty(_assemblyPrepareFilesDirectory))
            {
                _assemblyPrepareFilesDirectory = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".kprep");
            }
            
            if (_enableTrace) Console.WriteLine("AssemblyPrepareFilesDirectory {0}", _assemblyPrepareFilesDirectory);

            _assemblyCache = assemblyCache;
            _loaderImpl = loaderImpl;
            _loadFile = loadFile;
            _loadStream = loadStream;
        }

        //---------------------------------------------
        //Helpers
        public static string LocationFromAssembly(Assembly assembly)
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

        private static byte[] CalculateHash(byte[] data)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                return hashAlgorithm.ComputeHash(data);
            }
        }

        private static string CalculateHashFromString(string input)
        {
            string hashedString = null;
            byte[] bytes = null;
            byte[] hashBytes = null;

            bytes = Encoding.UTF8.GetBytes(input);
            if (bytes == null)
            {
                return null;
            }
            
            hashBytes = CalculateHash(bytes);
            if (hashBytes == null)
            {
                return null;
            }
            foreach (byte x in hashBytes)
            {
                hashedString += String.Format(CultureInfo.InvariantCulture, "{0:x2}", x);
            }

            return hashedString;
        }

        //---------------------------------------------
        //Startup Profile Path
        private static string _startupProfileDirectoryPath = "";
        
        public static void SetStartupProfilePath(string startupProfileDirectoryPath)
        {
            if (_enableTrace) Console.WriteLine("SetStartupProfilePath {0}", startupProfileDirectoryPath);
            _startupProfileDirectoryPath = startupProfileDirectoryPath;
        }
        
        public static string GetStartupProfilePath()
        {
            return _startupProfileDirectoryPath;
        }

        static string _applicationBinDirectoryName = "bin";
        static string _startupProfileDirectoryName = ".kstartup";
#if ASPNETCORE50
        static string _startupProfileFrameworkDirectoryName = "ASPNETCORE50";
#else
        static string _startupProfileFrameworkDirectoryName = "NET45";
#endif

        public static bool ComputeStartupProfilePath()
        {
#if ASPNETCORE50
            string applicationBaseDirectory = AppContext.BaseDirectory;
#else
            // REVIEW: Need a way to set the application base on mono
            //@@PlatformHelper.IsMono ? 
            //@@Directory.GetCurrentDirectory() : 
            string applicationBaseDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
#endif
            var applicationBinDirectory = Path.Combine(applicationBaseDirectory, _applicationBinDirectoryName);

            //@@TODO add support for Azure Websites "data" directory

            //@@ Can have compound param .Combine call ?
            var applicationStartupProfileDirectory = Path.Combine(applicationBinDirectory, _startupProfileDirectoryName);
            applicationStartupProfileDirectory = Path.Combine(applicationStartupProfileDirectory, _startupProfileFrameworkDirectoryName);
            var processorArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            applicationStartupProfileDirectory = Path.Combine(applicationStartupProfileDirectory, processorArchitecture);

            SetStartupProfilePath(applicationStartupProfileDirectory);
            if (!Directory.Exists(applicationStartupProfileDirectory))
            {
                try 
                {
                    Directory.CreateDirectory(applicationStartupProfileDirectory);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            
            return true;
        }

        // Article that describes usage of RuntimeHelpers.PrepareMethod 
        // http://blog.liranchen.com/2010/08/forcing-jit-compilation-during-runtime.html

        //---------------------------------------------
        //AssemblyProfile

        //PreloadAssembliesTimer
        private static Timer _assemblyProfile_PreloadAssembliesTimer = null;
        
        //PreloadFilePaths
        private static IEnumerable<string> _assemblyProfile_PreloadFilePaths = null;

        //WriteToFileTimer
        private static Timer _assemblyProfile_WriteToFileTimer = null;
        
        //Write State
        private static string _assemblyProfile_DirectoryPath = "";
        private static ConcurrentQueue<string> _assemblyProfile_AssemblyPathRecordQueue = new ConcurrentQueue<string>();

        private static void AssemblyProfile_PreloadTimerCallback(Object stateInfo)
        {
            if (_assemblyProfile_PreloadFilePaths != null)
            {
                AssemblyProfile_Preload();
            }
            else
            {
                if (_enableTrace) Console.WriteLine("AssemblyProfile_PreloadTimerCallback {0}", _assemblyProfile_PreloadFilePaths.ToString());
            }
        }
        
        //AssemblyExcludeList
        static List<string> _excludeList = new List<string>() {
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

        private static void AssemblyProfile_Preload()
        {
            AssemblyProfile_Preload(_assemblyProfile_PreloadFilePaths);
        }
        
        private static void AssemblyProfile_Preload(IEnumerable<string> assemblyFilePaths)
        {
            List<Assembly> assembliesLoaded = new List<Assembly>();
            
#if ASPNETCORE50
            string niSuffix = ".ni.dll";
            int niSuffixLength = niSuffix.Length;
            string ilSuffix = ".dll";
#else
#endif 
            int lineCount = 0;

            if (_enableTrace) Console.WriteLine("AssemblyProfile_Preload Enter");

            foreach(var assemblyFilePath in assemblyFilePaths)
            {
                if (String.IsNullOrEmpty(assemblyFilePath))
                {
                    continue;
                }
                
                var schedulePreload = true;
                var useAssemblyFilePath = assemblyFilePath;

                schedulePreload = true;
                
                if (_excludeList.Contains(Path.GetFileNameWithoutExtension(useAssemblyFilePath)))
                {
                    schedulePreload = false;
                    //if (_enableTrace) Console.WriteLine("Skipping {0}", useAssemblyFilePath);
                    continue;
                }

                if (schedulePreload)
                {
                    lineCount++;
                    
                    {
                        Assembly assembly = null;
                        var threadId = Thread.CurrentThread.ManagedThreadId;
#if ASPNETCORE50
                        if (assemblyFilePath.EndsWith(niSuffix))
                        {
                            var newAssemblyFilePath = assemblyFilePath.Substring(0, assemblyFilePath.Length - niSuffixLength) + ilSuffix;
                            useAssemblyFilePath = newAssemblyFilePath;
                        }
#else
#endif
                        //if (_enableTrace) Console.WriteLine("TID {0:00} ~~ SCHEDULED {1}", threadId, useAssemblyFilePath);
                        var sw = Stopwatch.StartNew();
                        
                        //@@ trying to make compile happy: complains about _loadFile not being referenced
                        var loadFile = _loadFile;
                        if (loadFile != null)
                        {
                            try
                            {
                                assembly = loadFile(useAssemblyFilePath);
                            }
                            catch(Exception e)
                            {
                                if (_enableTrace) Console.WriteLine("TID {0:00} {1} EXCEPTION {2}", threadId, useAssemblyFilePath, e.ToString());
                            }
                        }
                        
                        if (assembly != null)
                        {
                            assembliesLoaded.Add(assembly);
                            Patch_AddToAssemblyCache(assembly);
                            sw.Stop();
                            //if (_enableTrace) Console.WriteLine("TID {0:00} LOADED || Elapsed {1}ms {2}", threadId, sw.ElapsedMilliseconds, useAssemblyFilePath);
                        }
                    }
                }
            }
            
            //Now that assemblies are loaded, Prepare the assemblies on Tasks
            foreach(var assembly in assembliesLoaded)
            {
                Task.Factory.StartNew(_ =>
                {
                    var name = assembly.GetName().Name;
                    AssemblyPrepare_ScheduleAssembly(name, assembly, true);
                }
                ,
                null,
                CancellationToken.None,
                TaskCreationOptions.None, 
                TaskScheduler.Default); 
            }
            
            if (_enableTrace) Console.WriteLine("AssemblyProfile_Preload Exit - Assemblies Loaded || {0}", lineCount);
        }


        public static void AssemblyProfile_RecordQueueAdd(string assemblyFilepath)
        {
            if (!_enableAssemblyProfileRecordPlayback)
                return;
        
            // Add to in memory ordered queue and flush NN seconds after process has started
            if (_assemblyProfile_AssemblyPathRecordQueue != null)
            {
                _assemblyProfile_AssemblyPathRecordQueue.Enqueue(assemblyFilepath);
            }
        }
        
        public static void AssemblyProfile_RecordPlayback_Start()
        {
            if (!_enableAssemblyProfileRecordPlayback)
                return;
        
            if (_enableTrace) Console.WriteLine("AssemblyProfile_RecordPlayback_Start Enter");
        
            var assemblyProfileFilename = "project-assemblies.ini";
            var assemblyProfileDirectoryPath = Path.Combine(_startupProfileDirectoryPath, assemblyProfileFilename);
            _assemblyProfile_DirectoryPath = assemblyProfileDirectoryPath;
            
            if (File.Exists(assemblyProfileDirectoryPath))
            {
                //@@string projectJsonFilename = "project.json";
                //@@TODO make sure KRE directory version is unchanged - compute simple HASH on directoryname
                //@@TODO make sure project.json is unchanged - compute HASH on content
                //@@TODO make sure global.json is unchanged - compute HASH on content
                
                var lines = File.ReadLines(assemblyProfileDirectoryPath);
                if (lines != null)
                {
                    _assemblyProfile_PreloadFilePaths = lines;
                    
                    if (_enableTrace) Console.WriteLine("File.ReadLines {0}", lines.ToString());

                    AssemblyProfile_RecordPlayback_SchedulePreloadTimer();
                }
                else
                {
                    if (_enableTrace) Console.WriteLine("File.ReadLines returned null");
                }
            }
            else
            {
                if (_enableTrace) Console.WriteLine("profileDirectoryPath File.Exist FALSE {0}", assemblyProfileDirectoryPath);
            }

            //@@ is this switch still needed?
            var enableAssemblyProfileWrite = true;
            if (Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PROFILE_WRITE") == "0")
            {
                enableAssemblyProfileWrite = false;
            }
            
            if (enableAssemblyProfileWrite)
            {
                AssemblyProfile_ScheduleWriteToFileTimer();
            }

            if (_enableTrace) Console.WriteLine("AssemblyProfile_RecordPlayback_Start Exit");
        }

        public static void AssemblyProfile_RecordPlayback_SchedulePreloadTimer()
        {
            //Initialize the timer to not start automatically... 
            var timerCallback = new TimerCallback(AssemblyProfile_PreloadTimerCallback);
            var timer = new Timer(timerCallback, 
                                            null, 
                                            System.Threading.Timeout.Infinite,  //dueTime
                                            System.Threading.Timeout.Infinite); //period
            if (timer != null)
            {
                _assemblyProfile_PreloadAssembliesTimer = timer;
                
                //Manually start the timer... 
                //@@ move to defaults section
                var dueTime = 100; //100ms
                
                var preloadAssemblyProfileDelayInMilliseconds = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PROFILE_PRELOAD_DELAY");
                if ((preloadAssemblyProfileDelayInMilliseconds != null) && !String.IsNullOrEmpty(preloadAssemblyProfileDelayInMilliseconds))
                {
                    int newDueTime = -1;
                    if (Int32.TryParse(preloadAssemblyProfileDelayInMilliseconds, out newDueTime))
                    {
                        dueTime = newDueTime;
                    }
                }
                
                timer.Change(dueTime, System.Threading.Timeout.Infinite); 
                
                if (_enableTrace) Console.WriteLine("AssemblyProfile_RecordPlayback_SchedulePreloadTimer");
            }
        }

        public static void AssemblyProfile_ScheduleWriteToFileTimer()
        {
            //Initialize the timer to not start automatically... 
            var timerCallback = new TimerCallback(AssemblyProfile_WriteToFileTimerCallback);
            var timer = new Timer(timerCallback, 
                                            null, 
                                            System.Threading.Timeout.Infinite,  //dueTime
                                            System.Threading.Timeout.Infinite); //period
            if (timer != null)
            {
                _assemblyProfile_WriteToFileTimer = timer;
                
                //Manually start the timer... 
                //@@ move to defaults section
                var dueTime = 15*1000;
                
                var writeAssemblyProfileDelayInMilliseconds = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_ASSEMBLY_PROFILE_WRITE_DELAY");
                if ((writeAssemblyProfileDelayInMilliseconds != null) && !String.IsNullOrEmpty(writeAssemblyProfileDelayInMilliseconds))
                {
                    int newDueTime = -1;
                    if (Int32.TryParse(writeAssemblyProfileDelayInMilliseconds, out newDueTime))
                    {
                        dueTime = newDueTime;
                    }
                }
                
                timer.Change(dueTime, System.Threading.Timeout.Infinite); 
                
                if (_enableTrace) Console.WriteLine("AssemblyProfile_ScheduleWriteToFileTimer");
            }
        }

        public static void AssemblyProfile_WriteToFileTimerCallback(Object stateInfo)
        {
            AssemblyProfile_WriteToFile();
        }
        
        public static void AssemblyProfile_WriteToFile()
        {
            if (_assemblyProfile_AssemblyPathRecordQueue != null)
            {
                if (_enableTrace) Console.WriteLine("AssemblyProfile_WriteToFile Queue Count: {0}", _assemblyProfile_AssemblyPathRecordQueue.Count);
            
                TextWriter assemblyProfileWriter = new StreamWriter(new FileStream(_assemblyProfile_DirectoryPath, FileMode.Create));
                if (assemblyProfileWriter != null)
                {
                    int lineCount =0;
                    string line = "";
                    List<string> writtenList = new List<string>();
                    
                    while (_assemblyProfile_AssemblyPathRecordQueue.TryDequeue(out line)) 
                    {
                        //Check if already written
                        
                        //Dedup
                        if (writtenList.Contains(line))
                        {
                            continue;
                        }
                        
                        if (String.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        writtenList.Add(line);
                        
                        assemblyProfileWriter.WriteLine(line);
                        lineCount++;
                    }

                    assemblyProfileWriter.Flush();
                    assemblyProfileWriter.Dispose();
                    assemblyProfileWriter = null;
                    
                    if (_enableTrace) Console.WriteLine("AssemblyProfile_WriteToFile Write LineCount: {0}", lineCount);
                }
             }
             else
             {
                if (_enableTrace) Console.WriteLine("AssemblyProfile_WriteToFile _assemblyProfile_AssemblyPathRecordQueue is null");
             }
        }

        public static void Patch_AddToAssemblyCache(Assembly assembly)
        {
            if (assembly == null)
                return;

            try
            {
                if (_loadStream == null)
                    return;
                
                string name = assembly.GetName().Name;
                
                _assemblyCache[name] = assembly;

                // Skip loading interfaces for dynamic assemblies
                if (assembly.IsDynamic)
                {
                    return;
                }

                if (String.IsNullOrEmpty(LocationFromAssembly(assembly)))
                {
                    return;
                }

                RuntimeBootstrapper.ExtractAssemblyNeutralInterfaces(assembly, _loadStream);
                if (_enableTrace) Console.WriteLine("TID {0:00} {1} Patch_AddToAssemblyCache {2}", 
                                Thread.CurrentThread.ManagedThreadId,
                                DateTime.Now.ToString("HH:mm:ss.fff"),
                                name);  
            }
            catch(Exception e)
            {
                if (_enableTrace) Console.WriteLine("Patch_AddToAssemblyCache Exception {0}", e.ToString());
            }
        }

        //@@ Review if this concurrent tweak - help / hurt / no effect?
        private static int _concurrencyLevel = 4 * 64;

        private static int _initialCapacity = 64;
        private static readonly ConcurrentDictionary<string, Assembly> _assemblyPrepare_ForceLoadAssemblyCache = new ConcurrentDictionary<string, Assembly>(_concurrencyLevel, _initialCapacity);
        private static readonly ConcurrentDictionary<string, Assembly> _assemblyPrepare_PreJITMethodsAssembliesCache = new ConcurrentDictionary<string, Assembly>(_concurrencyLevel, _initialCapacity);

        // Recursively load all of assemblies referenced by the given assembly
        private static void AssemblyPrepare_ForceLoadReferencedAssembliesInternal(Assembly assembly)
        {
            string name = assembly.GetName().Name;
            
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
            if (String.IsNullOrEmpty(LocationFromAssembly(assembly)))
            {
                return;
            }
#endif

            if (_assemblyPrepare_ForceLoadAssemblyCache.ContainsKey(assembly.GetName().Name))
            {
                return;
            }
            
            if (_assemblyPrepare_ForceLoadAssemblyCache.TryAdd(name, assembly))
            {
                AssemblyName[] referencedAssemblies = null;
                
                AssemblyProfile_RecordQueueAdd(LocationFromAssembly(assembly));

                Patch_AddToAssemblyCache(assembly);
            
                try
                {
#if ASPNETCORE50
                    //@@TODO referencedAssemblies = assembly.GetReferencedAssemblies();
#else
                    referencedAssemblies = assembly.GetReferencedAssemblies();
#endif
                }
                catch(Exception excepRefAssemblies)
                {
                    if (_enableTrace) Console.WriteLine("AssemblyPrepare_ForceLoadReferencedAssemblies.GetReferencedAssemblies Exception {0}", excepRefAssemblies.ToString());
                }

                if (referencedAssemblies == null)
                {
                    return;
                }
                
                //Loop through ReferencedAssemblies
                foreach (AssemblyName curAssemblyName in referencedAssemblies)
                {
                    try 
                    {
                        Assembly nextAssembly = Assembly.Load(curAssemblyName);

                        AssemblyPrepare_ForceLoadReferencedAssembliesInternal(nextAssembly);
                        
                    }
                    catch(Exception excepLoad)
                    {
                        if (_enableTrace) Console.WriteLine("AssemblyPrepare_ForceLoadReferencedAssemblies.Load Exception {0}", excepLoad.ToString());
                    }
                }
            }
        }

        // Recursively force load all of assemblies referenced by the given assembly
        public static void AssemblyPrepare_ForceLoadReferencedAssemblies(Assembly assembly)
        {
            if (assembly == null)
                return;

            //if (_enableTrace) Console.WriteLine("AssemblyPrepare_ForceLoadReferencedAssemblies Enter {0}", LocationFromAssembly(assembly));
            var sw = Stopwatch.StartNew();
            AssemblyPrepare_ForceLoadReferencedAssembliesInternal(assembly);
            sw.Stop();
            //if (_enableTrace) Console.WriteLine("AssemblyPrepare_ForceLoadReferencedAssemblies Exit Elapsed {0}ms {1}", sw.ElapsedMilliseconds, LocationFromAssembly(assembly));
        }

        public static void AssemblyPrepare_PreJITMethods(Assembly assembly)
        {
#if ASPNETCORE50
#else
            if (assembly == null)
                return;

            // Skip dynamic assemblies? Revisit may be good for cold start latency ?
            if (assembly.IsDynamic)
            {
                if (_enableTrace) Console.WriteLine("AssemblyPrepare_PreJITMethods IsDynamic SKIP {0}", assembly.GetName().Name);
                return;
            }
            
            //Check if already done PreJIT on this assembly
            if (_assemblyPrepare_PreJITMethodsAssembliesCache.ContainsKey(assembly.GetName().Name))
            {
                if (_enableTrace) Console.WriteLine("AssemblyPrepare_PreJITMethods ALREADY PreJIT {0}", assembly.GetName().Name);
                return;
            }
            
            if (_assemblyPrepare_PreJITMethodsAssembliesCache.TryAdd(assembly.GetName().Name, assembly))
            {
                var candidateMethods = new List<MethodInfo>();
                
                try
                {
                    int countTypesTotal = 0;
                
                    //@@cache in static fields?
                    var assemblyMscorlib = typeof(object).GetTypeInfo().Assembly;
                    if (assemblyMscorlib == null)
                        return;

                    var typeRuntimeHelpers = assemblyMscorlib.GetType("System.Runtime.CompilerServices.RuntimeHelpers");
                    if (typeRuntimeHelpers == null)
                        return;

                    var methodPrepareMethod = typeRuntimeHelpers.GetTypeInfo().GetDeclaredMethods("PrepareMethod").FirstOrDefault();
                    if (methodPrepareMethod == null)
                        return;

                    if (!String.IsNullOrEmpty(assembly.Location))
                    {
                        var prepareMethodFileExtension = ".prepareMethods";
                        var assemblyDirectoryPath = Path.GetDirectoryName(assembly.Location);
                        
                        var prepareDirectoryPath = _assemblyPrepareFilesDirectory;

                        //@@TODO next assemblyDirectoryPath?
                        var prepareMethodFilepath = Path.Combine(prepareDirectoryPath, Path.GetFileNameWithoutExtension(assembly.Location) + prepareMethodFileExtension);
                        
                        if (File.Exists(prepareMethodFilepath))
                        {
                            var sepArray = new Char [] {','};
                            var lastTypeName = "";
                            Type lastType = null;

                            if (_enableTrace) Console.WriteLine("File.Exists TRUE $$$$$ {0} for {1}", prepareMethodFilepath, assembly.GetName().Name);
                            
                            var types_methods_lines = File.ReadLines(prepareMethodFilepath);
                            
                            if (_enableTrace) Console.WriteLine("File.ReadLines COUNT {0} for {1}",types_methods_lines.Count(), assembly.GetName().Name);

                            var swMethodInfosFromPrepareFile = Stopwatch.StartNew();
                            
                            foreach(var types_methods_line in types_methods_lines)
                            {
                                try
                                {
                                    Type currentType = null;
                                
                                    var lineParts = types_methods_line.Split(sepArray);
                                    if ((lineParts == null) || (lineParts.Length < 2))
                                        continue;
                                    
                                    var newTypeName = lineParts[0];
                                    var newMethodName = lineParts[1];

                                    //@@TODO add Support for Dictionary / Hashtable
                                    if ((lastType != null) && (String.Compare(lastTypeName, newTypeName, StringComparison.OrdinalIgnoreCase) == 0))
                                    {
                                        currentType = lastType;
                                    }
                                    else
                                    {
                                        currentType = assembly.GetType(newTypeName);
                                    }
                                    
                                    if (currentType == null)
                                    {
                                        continue;
                                    }
                                    
                                    lastType = currentType;
                                    lastTypeName = newTypeName;

                                    var currentMethods = currentType.GetTypeInfo().GetDeclaredMethods(newMethodName);
                                    if (currentMethods == null)
                                        continue;
                       
                                    foreach(var currentMethod in currentMethods)
                                    {
                                        //Skipped certain method types
                                        if (currentMethod.IsAbstract || currentMethod.ContainsGenericParameters)
                                            continue;
                                            
                                        candidateMethods.Add(currentMethod);
                                    }
                                }
                                catch(Exception ee)
                                {
                                    if (_enableTrace) Console.WriteLine("foreach EXCEPTION !!!!! {0}", ee.ToString());
                                }
                            }

                            swMethodInfosFromPrepareFile.Stop();
                            if (_enableTrace) Console.WriteLine("Found ~~~~~ {0} MethodsInfos from {1} Elapsed {2:000}ms for {3}",
                                        candidateMethods.Count, 
                                        prepareMethodFilepath,
                                        swMethodInfosFromPrepareFile.ElapsedMilliseconds, 
                                        assembly.GetName().Name);
                        }
                        else
                        {
                            //Console.WriteLine("File.Exists FALSE ##### {0}", prepareMethodFilepath);
                        }
                    }
                    
                    if (candidateMethods.Count == 0)
                    {
                        //Phase 1 - build list of methods from types in assembly
                        Type[] types = assembly.GetTypes();
                        countTypesTotal = types.Length;
                        int countMethodsTotal=0;

                        if (_enableTrace) Console.WriteLine("TID {0:00} {1} AssemblyPrepare_PreJITMethods Enter Types.Count {2} {3}",
                                            Thread.CurrentThread.ManagedThreadId,
                                            DateTime.Now.ToString("HH:mm:ss.fff"),
                                            countTypesTotal, 
                                            assembly.GetName().Name);

                        var swPhase1 = Stopwatch.StartNew();
                        
                        foreach (Type curType in types)
                        {
                            MethodInfo[] methods = curType.GetMethods(
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
                                    
                            countMethodsTotal += methods.Length;
                            
                            foreach (MethodInfo currentMethod in methods)
                            {
                                //Skipped certain method types
                                if (currentMethod.IsAbstract || currentMethod.ContainsGenericParameters)
                                    continue;
                                    
                                candidateMethods.Add(currentMethod);
                            }
                        }
                    }

                    //Func to PrepareMethods in a ListMethodInfo 
                    Func< List<MethodInfo>,string > methodPreparer = methodInfos =>
                    {
                        //phase 3 - PrepareMethods
                        int countMethodsPrepared = 0;
                        int countMethodsPreparedException = 0;

                        var swMethodInfos = Stopwatch.StartNew();
                        if (_enableTrace) Console.WriteLine("methodPreparer methodInfos.Count={0} {1}", methodInfos.Count, assembly.GetName().Name);

                        foreach(var methodInfo in methodInfos)
                        {
                            if (methodInfo == null)
                                continue;
                                
                            //@@TODO Add .PrepareMethod in CoreCLR
                            //Reflection: RuntimeHelpers.PrepareMethod(currentMethod.MethodHandle);
                            try
                            {
                                var args = new object[1] { methodInfo.MethodHandle };
                                var result = methodPrepareMethod.Invoke(null, args);
                                countMethodsPrepared++;
                            }
                            catch(Exception e)
                            {
                                var eString = e.ToString();
                                //if (_enableTrace) Console.WriteLine("{0}", e.ToString());
                                countMethodsPreparedException++;
                            }
                        }
                        
                        swMethodInfos.Stop();
                        if (_enableTrace) Console.WriteLine("TID {0:00} {1} AssemblyPrepare_PreJITMethods %%%%% Exit Types.Count {2:000} Methods {3:000}/{4:000} Elapsed {5:000}ms {6}",
                                    Thread.CurrentThread.ManagedThreadId,
                                    DateTime.Now.ToString("HH:mm:ss.fff"),
                                    countTypesTotal,
                                    countMethodsPrepared,
                                    methodInfos.Count, 
                                    swMethodInfos.ElapsedMilliseconds, 
                                    assembly.GetName().Name);
                                    
                        return "";
                    };
                    
                    //phase 2 - Schedule PrepareMethod chunks
                    if (_enableTrace) Console.WriteLine("phase 2 - schedule PrepareMethod chunks - candidateMethods.Count {0} for {1}", candidateMethods.Count, assembly.GetName().Name);
                    
                    //@@move to const section in class
                    int defaultChunkSize = 2000;
                    
                    //if small method count schedule inline
                    if (candidateMethods.Count <= defaultChunkSize)
                    {
                        // Run inline (on current thread)
                        methodPreparer(candidateMethods);
                    }
                    else
                    {
                        //Else - Chunk up large list and schedule on child Tasks

                        //List to keep trace of the individual chunked lists
                        List<List<MethodInfo>> listChunks = new List<List<MethodInfo>>();
                        
                        //Chunk master list
                        int index=0;
                        while (index < candidateMethods.Count)
                        {
                            List<MethodInfo> candidateMethodsChunkLocal = null;
                            
                            // if (Count - index) is greater than defaultChunkSize then 
                            // use defaultChunkSize 
                            // else use the remaining smaller chunk
                            
                            var chunkSize = ((candidateMethods.Count - index) > defaultChunkSize) ? defaultChunkSize : candidateMethods.Count - index;

                            candidateMethodsChunkLocal = candidateMethods.GetRange(index, chunkSize);
                            listChunks.Add(candidateMethodsChunkLocal);

                            index += chunkSize;
                        }
                    
                        //Schedule Tasks to process chunks
                        var  tasks  = new List<Task>();
                        foreach(var candidateMethodsChunk in listChunks)
                        {
                            tasks.Add(Task.Factory.StartNew(_ =>
                            {
                                if (_enableTrace) Console.WriteLine("Schedule chunk candidateMethodsChunk.Count {1} {2}", 
                                                index,
                                                candidateMethodsChunk.Count,
                                                assembly.GetName().Name
                                                );
                                methodPreparer(candidateMethodsChunk);
                            }
                            ,
                            null,
                            CancellationToken.None,
                            TaskCreationOptions.None, 
                            TaskScheduler.Default)); 
                        }
                        
                        //Wait for Tasks to complete
                        var swScheduleChunks = Stopwatch.StartNew();
                        if (_enableTrace) Console.WriteLine("Schedule chunks Task.WaitAll {0}", assembly.GetName().Name);
                        Task.WaitAll(tasks.ToArray()); 
                        
                        swScheduleChunks.Stop();
                        if (_enableTrace) Console.WriteLine("Schedule chunks Task.WaitAll Elapsed {0:000}ms {1}", 
                                        swScheduleChunks.ElapsedMilliseconds, 
                                        assembly.GetName().Name);
                    }
                }
                catch(ReflectionTypeLoadException rtle)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach(var excep in rtle.LoaderExceptions)
                    {
                        sb.Append(excep.ToString());
                        sb.Append(@"\t\r\n");
                    }
                
                    if (_enableTrace) Console.WriteLine("AssemblyPrepare_PreJITMethods ReflectionTypeLoadException {0} {1} {2}", 
                                        rtle.ToString(), 
                                        sb.ToString(),
                                        assembly.GetName().Name);
                }
                catch(Exception e)
                {
                    if (_enableTrace) Console.WriteLine("AssemblyPrepare_PreJITMethods Exception {0} {1}", e.ToString(), assembly.GetName().Name);
                }
            }
#endif
        }

        public static void AssemblyPrepare_ScheduleAssembly(string name, Assembly assembly)
        {
            AssemblyPrepare_ScheduleAssembly(name, assembly, false);
        }

        // var types = Assembly.Load("FooAssembly, Version=1.2.3.4, Culture=neutral, PublicKeyToken=00000000000000").GetTypes()
        public static void AssemblyPrepare_ScheduleAssembly(string name, Assembly assembly, bool performWorkDirectlyOnCurrentThread)
        {
            if (!_enableAssemblyPrepare)
                return;
        
            if (assembly == null)
                return;

            if (_enableTrace) Console.WriteLine("AssemblyPrepare_ScheduleAssembly ENTER {0} {1}", assembly.GetName().Name, LocationFromAssembly(assembly));
        
            var schedulePrepareAssembly = true;
            
            //@@TODO Check for .in.dll extension
            
             
            if (_excludeList.Contains(Path.GetFileNameWithoutExtension(LocationFromAssembly(assembly))))
            {
                schedulePrepareAssembly = false;
            }
            
            if (schedulePrepareAssembly)
            {
               var assemblyLocation = LocationFromAssembly(assembly);
               if (_enableTrace) Console.WriteLine("AssemblyPrepare_ScheduleAssembly PREP {0} {1}", assembly.GetName().Name, assemblyLocation);
               
               if (performWorkDirectlyOnCurrentThread)
               {
                    AssemblyPrepare_ForceLoadReferencedAssemblies(assembly);
#if ASPNETCORE50
                    //@@TODO PrepareMethod in CoreCLR ?
#else
                    AssemblyPrepare_PreJITMethods(assembly);
#endif
               }
               else
               {
                    Task.Factory.StartNew(_ =>
                    {
                        AssemblyPrepare_ForceLoadReferencedAssemblies(assembly);
                        AssemblyPrepare_PreJITMethods(assembly);
                    }
                    ,
                    null,
                    CancellationToken.None,
                    TaskCreationOptions.None, 
                    TaskScheduler.Default); 
               }
            }
            else
            {
                //if (_enableTrace) Console.WriteLine("AssemblyPrepare_ScheduleAssembly SKIP {0} {1}", assembly.ToString(), LocationFromAssembly(assembly));
            }
        }

        //---------------------------------------------
        //MultiCoreJit
        private static Timer _multiCoreJitTimer = null;
        private static int _multiCoreJitTimerCount = 0;
        private static int _multiCoreJitTimerCountMax = 3;

        private static void MultiCoreJit_FlushTimerCallback(Object stateInfo)
        {
            _multiCoreJitTimerCount++;
            var profileFilename = String.Format("startup.{0:D2}.prof", _multiCoreJitTimerCount );
            
            MultiCoreJit_StartProfile(profileFilename);
            
            //Have reached iteration limit?
            if (_multiCoreJitTimerCount >= _multiCoreJitTimerCountMax)
            {
                //Manually stop the timer... 
                _multiCoreJitTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        
        private static void MultiCoreJit_StartTimer(int dueTime, int period)
        {
            //Initialize the timer to not start automatically... 
            var timerCallback = new TimerCallback(MultiCoreJit_FlushTimerCallback);
            var timer = new Timer(timerCallback, 
                                            null, 
                                            System.Threading.Timeout.Infinite,  //dueTime 
                                            System.Threading.Timeout.Infinite); //period
            //Store for later use
            _multiCoreJitTimer = timer;
            
            //Manually start the timer... 
            timer.Change(dueTime, period); 
        }
        
        public static void MultiCoreJit_StartProfile(string profileFilename)
        {
            if (_enableTrace) Console.WriteLine("MultiCoreJit_StartProfile " + profileFilename);
#if ASPNETCORE50
            _loaderImpl.StartMultiCoreJitProfile(profileFilename);
#else
            //Reflection
            var assemblyMscorlib = typeof(object).GetTypeInfo().Assembly;
            if (assemblyMscorlib != null)
            {
                var typeProfileOptimization = assemblyMscorlib.GetType("System.Runtime.ProfileOptimization");
                if (typeProfileOptimization != null) 
                {
                    var methodStartProfileMethod = typeProfileOptimization.GetTypeInfo().GetDeclaredMethods("StartProfile").FirstOrDefault();
                    if (methodStartProfileMethod != null)
                    {
                        var args = new object[1] { profileFilename };
                        var result = methodStartProfileMethod.Invoke(null, args);
                        
                        if (_enableTrace) Console.WriteLine("System.Runtime.ProfileOptimization.StartProfile {0}", profileFilename);
                    }
                }
            }
#endif
        }
        
        public static void MultiCoreJit_Start()
        {
            //@@ move to const section
            var multiCoreJitTimerDueTime = 20*1000; // next time the timer fires
            var multiCoreJitTimerPeriod = 10*000;   // time interval timer fires
        
            var multiCoreJitStartupProfileFilename = "startup.00.prof";
        
            var enableMultiCoreJit = Environment.GetEnvironmentVariable("KRE_OPTIMIZER_MCJ") == "1";
            if (enableMultiCoreJit)
            {
#if ASPNETCORE50
                if (_loaderImpl.EnableMultiCoreJit(GetStartupProfilePath()))
                {
                    MultiCoreJit_StartProfile(multiCoreJitStartupProfileFilename);
                    MultiCoreJit_StartTimer(
                                            multiCoreJitTimerDueTime,  // dueTime - next time the timer fires
                                            multiCoreJitTimerPeriod);  // period - time interval timer fires
                }
#else
                //Reflection
                var assemblyMscorlib = typeof(object).GetTypeInfo().Assembly;
                if (assemblyMscorlib != null)
                {
                    var typeProfileOptimization = assemblyMscorlib.GetType("System.Runtime.ProfileOptimization");
                    if (typeProfileOptimization != null) 
                    {
                        var methodStartProfileMethod = typeProfileOptimization.GetTypeInfo().GetDeclaredMethods("SetProfileRoot").FirstOrDefault();
                        if (methodStartProfileMethod != null)
                        {
                            var args = new object[1] { GetStartupProfilePath() };
                            var result = methodStartProfileMethod.Invoke(null, args);
                            
                            if (_enableTrace) Console.WriteLine("System.Runtime.ProfileOptimization.SetProfileRoot {0}", GetStartupProfilePath());
                        }
                    }
                }

                MultiCoreJit_StartProfile(multiCoreJitStartupProfileFilename);
                MultiCoreJit_StartTimer(
                                        multiCoreJitTimerDueTime,  // dueTime
                                        multiCoreJitTimerPeriod);  // period (interval)

#endif
            }
        }
    }
}