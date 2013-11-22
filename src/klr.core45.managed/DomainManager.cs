// From: Jan Kotas (CLR)
// Updates: jhawk

//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//            ! ! ! ! ! ! ! WARNING ! ! ! ! ! ! ! !
//
//    This code is a Managed Host for CoreCLR and therefore has some
//    special restrictions:
//
//   * the project must have TargetFrameworkVersion = v4.5. This
//     ensures it is built against mscorlib.dll. Ideally it would
//     be built against the CoreCLR mscorlib.dll, however that is
//     not available in the build, so the 4.5 version is used.
//   * no assemblies may be explicitly referenced
//   * some referenced types and/or members may not exist at
//     at run time. This is byproduct of building against the 
//     4.5 mscorlib.dll instead of the CoreCLR mscorlib.dll.
//
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

#define ENABLE_HOSTDATA_LOGGING

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;

/// <summary>
/// Implements the managed AppDomainManager part of a CoreCLR host. 
/// The native host uses this class as an AppDomainManager, and
/// calls into it to start an application running.
/// </summary>
[SecurityCritical]
sealed class DomainManager : AppDomainManager
{
    //static bool m_fTraceVerbose = false;
    static bool m_fTraceVerbose = true;

    static String[] m_UserApplicationTypeNameDefaults = { "Application", "Start", "Startup" };
    static String m_UserApplicationTypeName = "";

    static String m_UserApplicationMethodNameStartup = "Startup";
    static String m_UserApplicationMethodNameMain = "Main";
    static String m_UserApplicationMethodNameShutdown = "Shutdown";

    static String m_UserApplicationAssemblyName = "";
    static Assembly m_UserApplicationAssembly = null;
    static Type m_UserApplicationType = null;

#if ENABLE_HOSTDATA_LOGGING
    static PrivateMethods s_privateMethods;
#endif // ENABLE_HOSTDATA_LOGGING

    public DomainManager()
    {
        // First call into the managed host code when the CoreCLR starts.
        Log("CoreCLRHostAppDomainManager()");
    }

    public override bool CheckSecuritySettings(SecurityState state)
    {
        // Allow anything
        return true;
    }

    public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
    {
        Log("CoreCLRHostAppDomainManager.InitializeNewDomain({0})", appDomainInfo);
        Log("CoreCLRHostAppDomainManager.ApplicationName: {0}", appDomainInfo.ApplicationName);
        Log("CoreCLRHostAppDomainManager.LoaderOptimization: {0}", appDomainInfo.LoaderOptimization);

        base.InitializeNewDomain(appDomainInfo);
    }

    // Performs steps 
    //  1) load and find assembly 
    //  2) find type 
    //  3) find method
    //  4) invoke method
    [SecurityCritical]
    static bool TryExecuteAssemblyTypeMethod(string methodName, string[] nativeArgs, out int exitCode)
    {
        MethodInfo methodToInvoke = null;

        // Prepare out param
        exitCode = -1;

#if ENABLE_HOSTDATA_LOGGING
        // Log some well known AppDomain data
        LogHostData("APP_PATHS");
        LogHostData("APPBASE");
        LogHostData("LOADER_OPTIMIZATION");
        LogHostData("LOCATION_URI");
        LogHostData("TRUSTED_PLATFORM_ASSEMBLIES");
        LogHostData("TRUSTEDPATH");
#endif // ENABLE_HOSTDATA_LOGGING

        Log("ApplicationAssemblyName: {0}", m_UserApplicationAssemblyName);
        Log("ApplicationTypeName: {0}", m_UserApplicationTypeName);
        Log("MethodName: {0}", methodName);

        //Load m_UserApplicationAssembly
        if (object.ReferenceEquals(m_UserApplicationAssembly, null))
        {
            //Prepare Assembly Name - Adjust the assembly name so that it an assembly name
            if (m_UserApplicationAssemblyName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // Strip off the .exe 
                m_UserApplicationAssemblyName = m_UserApplicationAssemblyName.Substring(0, m_UserApplicationAssemblyName.Length - 4);
            }
            else if (m_UserApplicationAssemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                // Strip off the .dll 
                m_UserApplicationAssemblyName = m_UserApplicationAssemblyName.Substring(0, m_UserApplicationAssemblyName.Length - 4);
            }

            if (m_UserApplicationAssemblyName == "")
            {
                Log("Specified assembly to run was not found: {0}", nativeArgs.Length > 1 ? nativeArgs[1] : m_UserApplicationAssemblyName);
                return false;
            }

            Log("Loading assembly: {0}", m_UserApplicationAssemblyName);

            //@@TODO move Assembly Loading to PackageLoader.
            // Load the assembly to execute. This will throw if the assembly can't be loaded.
            var assembly = Assembly.Load(m_UserApplicationAssemblyName);
            if (object.ReferenceEquals(assembly, null))
            {
                return false;
            }

            m_UserApplicationAssembly = assembly;
        }

        /*
        // Find - If the assembly contains an entry point with the correct signature
        //if looking for "Main" - look for assembly.EntryPoint
        if (String.Compare(methodName, m_UserApplicationMethodNameMain, StringComparison.InvariantCultureIgnoreCase) == 0)
        {
            var moduleEntryPointMethod = m_UserApplicationAssembly.EntryPoint;
            if (object.ReferenceEquals(moduleEntryPointMethod, null))
            {
                Log("Assembly.EntryPoint Main method not found in Assembly.");
            }
            else
            {
                m_UserApplicationType = moduleEntryPointMethod.GetType();
                methodToInvoke = moduleEntryPointMethod;
            }
        }
        */

        //Find m_UserApplicationTypeName
        if (String.IsNullOrEmpty(m_UserApplicationTypeName) == false)
        {
            Type type = m_UserApplicationAssembly.GetType(m_UserApplicationTypeName);
            if (object.ReferenceEquals(type, null))
            {
                Log(String.Format("TypeName {0} not found", m_UserApplicationTypeName));
                return false;
            }
            else
            {
                Log(String.Format("TypeName {0} found", m_UserApplicationTypeName));
                m_UserApplicationType = type;
            }
        }
        else
            if (object.ReferenceEquals(m_UserApplicationType, null))
            {
                Log("Scanning for typenames");

                foreach (var entryPointTypeName in m_UserApplicationTypeNameDefaults)
                {
                    //Log(String.Format("Looking for TypeName: {0}", entryPointTypeName));

                    Type type = m_UserApplicationAssembly.GetType(entryPointTypeName);
                    if (object.ReferenceEquals(type, null))
                    {
                        //Log(String.Format("TypeName {0} not found", entryPointTypeName));
                        continue;
                    }
                    else
                    {
                        Log(String.Format("TypeName {0} found", entryPointTypeName));
                        m_UserApplicationTypeName = entryPointTypeName;
                        m_UserApplicationType = type;
                        break;
                    }
                }

                if (object.ReferenceEquals(m_UserApplicationType, null))
                {
                    Log(String.Format("XXX EntryPointType not found"));
                    return false;
                }
            }

        //Find Method to Invoke via Reflection
        Log(String.Format("Looking for method {0} ", methodName));

        methodToInvoke = m_UserApplicationType.GetMethod(methodName);
        if (object.ReferenceEquals(methodToInvoke, null))
        {
            Log(String.Format("Method {0} not found in {1},{2}",
                    methodName,
                    m_UserApplicationType.ToString(),
                    m_UserApplicationAssembly
                    ));
            return false;
        }

        Log(String.Format("Method {0} found in {1}, {2}",
                methodName,
                m_UserApplicationType.ToString(),
                m_UserApplicationAssembly
                ));

        //Reflection - Invoke method
        var parameters = methodToInvoke.GetParameters();
        var parameterCount = parameters.Length;
        var returnParameterType = methodToInvoke.ReturnParameter.ParameterType;

        if ((parameterCount == 0)
            || (
                  (parameterCount == 1)
               && (parameters[0].ParameterType.Equals(typeof(string[])))
               )
            )
        {
            string[] arguments = null;
            string argumentsString = "";
            if (parameterCount == 1)
            {
                //@@ old code from ccrun
                /*
                // Pass all except the first argument. The first argument
                // is the command verb (i.e. the exe name).
                arguments = new string[nativeArgs.Length > 1 ? nativeArgs.Length - 1 : 0];
                for (var i = 0; i < arguments.Length; i++)
                {
                    arguments[i] = nativeArgs[i + 1];
                }
                */
                arguments = new string[nativeArgs.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    arguments[i] = nativeArgs[i];
                }

                argumentsString = "new string[]{";

                for (var i = 0; i < arguments.Length; i++)
                {
                    if (i > 0)
                    {
                        argumentsString += ", ";
                    }
                    argumentsString += "\"" + arguments[i] + "\"";
                }
                argumentsString += "}";

                Log("Invoking: {0} {1}.Main({2})",
                    returnParameterType.Name,
                    methodToInvoke.DeclaringType.FullName,
                    argumentsString);
            }

            object[] invokeArgs = arguments == null ? null : new object[] { arguments };
            if (returnParameterType.Equals(typeof(void)))
            {
                // void return type
                methodToInvoke.Invoke(null, invokeArgs);
                exitCode = Environment.ExitCode;
                return true;
            }
            else if (returnParameterType.Equals(typeof(int)))
            {
                // int return type
                exitCode = (int)methodToInvoke.Invoke(null, invokeArgs);
                return true;
            }
            else
            {
                // uint return type
                exitCode = (int)(uint)methodToInvoke.Invoke(null, invokeArgs);
                return true;
            }
        }

        // The entrypoint method does not have an acceptable signature.

        Log("Main method had incorrect signature");

        return false;
    }

    //Called by KSys!ClrDomainInstance
    unsafe static int HostStartup(
            int argc,
            char** argv,
            char* hostPath,
            char* applicationTypeName,
            char* applicationAssembly,
            byte verboseTrace,
            byte waitForDebugger,
            int* success
        )
    {
        int exitCode = 0;

        if (verboseTrace == 1)
            m_fTraceVerbose = true;
        else
            m_fTraceVerbose = false;

        Log("K.Core.Host.DomainManager.HostStartup called");

        if (waitForDebugger != 0)
        {
            WaitForDebugger();
        }

        var tempApplicationTypeName = new string(applicationTypeName);
        if (tempApplicationTypeName.Length > 0)
            m_UserApplicationTypeName = tempApplicationTypeName;

        var tempAssemblyName = new string(applicationAssembly);
        var directoryName = Path.GetDirectoryName(tempAssemblyName);

        if (String.IsNullOrEmpty(directoryName) == false)
        {
            m_UserApplicationAssemblyName = Path.GetFileNameWithoutExtension(tempAssemblyName);
        }
        else
        {
            m_UserApplicationAssemblyName = new string(applicationAssembly);
        }

        //Pack arguments for UserApplication
        var arguments = new string[argc];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = new string(argv[i]);
            Log("  {0}: \"{1}\"", i, arguments[i]);
        }

        *success = TryExecuteAssemblyTypeMethod(m_UserApplicationMethodNameStartup, arguments, out exitCode) ? 1 : 0;

        return exitCode;
    }

    /// <summary>
    /// The entrypoint called by the host.
    /// </summary>
    /// <param name="argc">The number of arguments in the argv array.</param>
    /// <param name="argv">Array of arguments as they were passed to the native host.</param>
    /// <param name="hostPath">The path to the native host module that is hosting the CoreCLR.</param>
    /// <param name="assemblyToRun">The managed executable to start running. This must have no
    /// path specified, but may have an extension. The assembly must contain a valid entry point.</param>
    /// <param name="success">Must be set non-zero to indicate success.</param>
    /// <param name="verbose">If true, log details to console.</param>
    /// <param name="waitForDebugger">If true, wait for a debugger to attach before continuing.</param>
    /// <returns>Exit code for process.</returns>

    //Called by KSys!ClrDomainInstance
    [HandleProcessCorruptedStateExceptions, SecurityCritical]
    unsafe static int HostMain(
            int argc,
            char** argv,
            int* success
        )
    {
        int exitCode = 0;

        Log("K.Core.Host.DomainManager.HostMain called");
        Log(" argument array length: {0}", argc);

        //Pack arguments
        var arguments = new string[argc];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = new string(argv[i]);
            Log("  {0}: \"{1}\"", i, arguments[i]);
        }

        *success = TryExecuteAssemblyTypeMethod(m_UserApplicationMethodNameMain, arguments, out exitCode) ? 1 : 0;

        return exitCode;
    }

    //Called by KSys!ClrDomainInstance
    unsafe static int HostShutdown(
            int* success
        )
    {
        int exitCode = 0;

        *success = TryExecuteAssemblyTypeMethod(m_UserApplicationMethodNameShutdown, null, out exitCode) ? 1 : 0;

        return exitCode;
    }

    //@@from original ccrun
    //@@TODO will need to rewrite this debugger waiting
    //@@ when running in w3wp ... can't "press enter" 
    static void WaitForDebugger()
    {
        if (!Debugger.IsAttached)
        {
            LogAlways("Waiting for the debugger to attach. Press enter to continue ...");

            Console.ReadLine();
            if (Debugger.IsAttached)
            {
                LogAlways("Debugger is attached.");
                Debugger.Break();
            }
            else
            {
                LogAlways("Debugger failed to attach.");
            }
        }
    }

#if ENABLE_HOSTDATA_LOGGING
    // Retrieves string data that was associated with the host when the AppDomain was created.
    static string GetHostData(AppDomain appDomain, string key)
    {
        return s_privateMethods.AppDomain_GetData(appDomain, key) as string;
    }
#endif // ENABLE_HOSTDATA_LOGGING

    static void LogAlways(string message)
    {
        Console.WriteLine(String.Format(" HOSTLOG: {0}", message));
        OutputDebugStringW(String.Format(" HOSTLOG: {0}", message));
    }

    static void Log(string message)
    {
        if (m_fTraceVerbose)
        {
            LogAlways(message);
        }
    }

    static void Log(string format, params object[] arguments)
    {
        Log(String.Format(CultureInfo.InvariantCulture, format, arguments));
    }

#if ENABLE_HOSTDATA_LOGGING
    static void LogHostData(string name)
    {
        Log("HostData: {0}={1}", name, GetHostData(AppDomain.CurrentDomain, name));
    }
#endif // ENABLE_HOSTDATA_LOGGING

    // Provides access to otherwise inaccessible methods in mscorlib. Uses reflection to find the
    // methods and caches the resulting MemberInfos.
    struct PrivateMethods
    {
        MethodInfo m_appDomainGetData;

        [SecurityCritical]
        internal object AppDomain_GetData(AppDomain appDomain, string keyName)
        {
            if (object.ReferenceEquals(m_appDomainGetData, null))
            {
                m_appDomainGetData = typeof(AppDomain).GetMethod(
                    name: "GetData",
                    bindingAttr: BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod);
            }
            return m_appDomainGetData.Invoke(appDomain, new[] { keyName });
        }

        [SecurityCritical]
        internal object AppDomain_AssemblyResolve(AppDomain appDomain)
        {
            appDomain.AssemblyResolve += appDomain_AssemblyResolve;
            return null;
        }

        Assembly appDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return null;
        }

    }

    //@@TODO move to a NativeMethods.cs
    [DllImport("kernel32.dll", ExactSpelling = true, EntryPoint = "OutputDebugStringW", CharSet = CharSet.Unicode)]
    public static extern void OutputDebugStringW(String outputString);
}
