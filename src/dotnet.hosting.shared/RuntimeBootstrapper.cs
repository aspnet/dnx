// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
#if ASPNETCORE50
using System.Threading;
using System.Runtime.Loader;
#endif
using System.Threading.Tasks;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace dotnet.hosting
{
    internal static class RuntimeBootstrapper
    {
        private static readonly ConcurrentDictionary<string, object> _assemblyLoadLocks =
                new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<string, Assembly> _assemblyCache
                = new ConcurrentDictionary<string, Assembly>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<string, Assembly> _assemblyNeutralInterfaces =
            new ConcurrentDictionary<string, Assembly>(StringComparer.Ordinal);

        private static readonly char[] _libPathSeparator = new[] { ';' };

        public static int Execute(string[] args)
        {
            // If we're a console host then print exceptions to stderr
            // TODO: remove KRE_ env var
            var printExceptionsToStdError = (Environment.GetEnvironmentVariable("DOTNET_CONSOLE_HOST") ?? Environment.GetEnvironmentVariable("KRE_CONSOLE_HOST")) == "1";

            try
            {
                return ExecuteAsync(args).Result;
            }
            catch (Exception ex)
            {
                if (printExceptionsToStdError)
                {
                    PrintErrors(ex);
                    return 1;
                }

                throw;
            }
        }

        private static void PrintErrors(Exception ex)
        {
            while (ex != null)
            {
                if (ex is TargetInvocationException ||
                    ex is AggregateException)
                {
                    // Skip these exception messages as they are
                    // generic
                }
                else
                {
                    Console.Error.WriteLine(ex);
                }

                ex = ex.InnerException;
            }
        }

        public static Task<int> ExecuteAsync(string[] args)
        {
            // TODO: remove KRE_ env var
            var enableTrace = (Environment.GetEnvironmentVariable("DOTNET_TRACE") ?? Environment.GetEnvironmentVariable("KRE_TRACE")) == "1";
#if ASPNET50
            // TODO: Make this pluggable and not limited to the console logger
            if (enableTrace)
            {
                var listener = new ConsoleTraceListener();
                Trace.Listeners.Add(listener);
                Trace.AutoFlush = true;
            }
#endif
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet";

            // RuntimeBootstrapper doesn't need to consume '--appbase' option because
            // dotnet/dotnet.cpp consumes the option value before invoking RuntimeBootstrapper
            // This is only for showing help info and swallowing useless '--appbase' option
            var optionAppbase = app.Option("--appbase <PATH>", "Application base directory path",
                CommandOptionType.SingleValue);
            var optionLib = app.Option("--lib <LIB_PATHS>", "Paths used for library look-up",
                CommandOptionType.MultipleValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", GetVersion());

            // Options below are only for help info display
            // They will be forwarded to Microsoft.Framework.ApplicationHost
            var optionsToForward = new[]
            {
                app.Option("--watch", "Watch file changes", CommandOptionType.NoValue),
                app.Option("--packages <PACKAGE_DIR>", "Directory containing packages", CommandOptionType.SingleValue),
                app.Option("--configuration <CONFIGURATION>", "The configuration to run under", CommandOptionType.SingleValue),
                app.Option("--port <PORT>", "The port to the compilation server", CommandOptionType.SingleValue)
            };

            app.Execute(args);

            // Help information was already shown because help option was specified
            if (app.IsShowingInformation)
            {
                return Task.FromResult(0);
            }

            // Show help information if no subcommand/option was specified
            if (!app.IsShowingInformation && !app.RemainingArguments.Any())
            {
                app.ShowHelp();
                return Task.FromResult(2);
            }

            // Some options should be forwarded to Microsoft.Framework.ApplicationHost
            var appHostName = "Microsoft.Framework.ApplicationHost";
            var appHostIndex = app.RemainingArguments.FindIndex(s =>
                string.Equals(s, appHostName, StringComparison.OrdinalIgnoreCase));
            foreach (var option in optionsToForward)
            {
                if (option.HasValue())
                {
                    if (appHostIndex < 0)
                    {
                        Console.WriteLine("The option '--{0}' can only be used with {1}", option.LongName, appHostName);
                        return Task.FromResult(1);
                    }

                    if (option.OptionType == CommandOptionType.NoValue)
                    {
                        app.RemainingArguments.Insert(appHostIndex + 1, "--" + option.LongName);
                    }
                    else if (option.OptionType == CommandOptionType.SingleValue)
                    {
                        app.RemainingArguments.Insert(appHostIndex + 1, "--" + option.LongName);
                        app.RemainingArguments.Insert(appHostIndex + 2, option.Value());
                    }
                    else if (option.OptionType == CommandOptionType.MultipleValue)
                    {
                        foreach (var value in option.Values)
                        {
                            app.RemainingArguments.Insert(appHostIndex + 1, "--" + option.LongName);
                            app.RemainingArguments.Insert(appHostIndex + 2, value);
                        }
                    }
                }
            }

            // Resolve the lib paths
            string[] searchPaths = ResolveSearchPaths(optionLib.Values, app.RemainingArguments);

            Func<string, Assembly> loader = _ => null;
            Func<Stream, Assembly> loadStream = _ => null;
            Func<string, Assembly> loadFile = _ => null;

            Func<AssemblyName, Assembly> loaderCallback = assemblyName =>
            {
                string name = assemblyName.Name;

                // Skip resource assemblies
                if (name.EndsWith(".resources"))
                {
                    return null;
                }

                // If the assembly was already loaded use it
                Assembly assembly;
                if (_assemblyCache.TryGetValue(name, out assembly))
                {
                    return assembly;
                }

                var loadLock = _assemblyLoadLocks.GetOrAdd(name, new object());
                try
                {
                    // Concurrently loading the assembly might result in two distinct instances of the same assembly 
                    // being loaded. This was observed when loading via Assembly.LoadStream. Prevent this by locking on the name.
                    lock (loadLock)
                    {
                        if (_assemblyCache.TryGetValue(name, out assembly))
                        {
                            // This would succeed in case the thread was previously waiting on the lock when assembly 
                            // load was in progress
                            return assembly;
                        }

                        assembly = loader(name) ?? ResolveHostAssembly(loadFile, searchPaths, name);

                        if (assembly != null)
                        {
#if ASPNETCORE50
                            ExtractAssemblyNeutralInterfaces(assembly, loadStream);
#endif
                            _assemblyCache[name] = assembly;
                        }
                    }
                }
                finally
                {
                    _assemblyLoadLocks.TryRemove(name, out loadLock);
                }

                return assembly;
            };
#if ASPNETCORE50
            var loaderImpl = new DelegateAssemblyLoadContext(loaderCallback);
            loadStream = assemblyStream => loaderImpl.LoadStream(assemblyStream, assemblySymbols: null);
            loadFile = path => loaderImpl.LoadFile(path);

            AssemblyLoadContext.InitializeDefaultContext(loaderImpl);

            if (loaderImpl.EnableMultiCoreJit())
            {
                loaderImpl.StartMultiCoreJitProfile("startup.prof");
            }
#else
            var loaderImpl = new LoaderEngine();
            loadStream = assemblyStream => loaderImpl.LoadStream(assemblyStream, assemblySymbols: null);
            loadFile = path => loaderImpl.LoadFile(path);

            ResolveEventHandler handler = (sender, a) =>
            {
                var appDomain = (AppDomain)sender;
                var afterPolicy = appDomain.ApplyPolicy(a.Name);
                if (afterPolicy != a.Name)
                {
                    return Assembly.Load(afterPolicy);
                }

                return loaderCallback(new AssemblyName(afterPolicy));
            };

            AppDomain.CurrentDomain.AssemblyResolve += handler;
            AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs loadedArgs) =>
            {
                // Skip loading interfaces for dynamic assemblies
                if (loadedArgs.LoadedAssembly.IsDynamic)
                {
                    return;
                }

                ExtractAssemblyNeutralInterfaces(loadedArgs.LoadedAssembly, loadStream);
            };
#endif

            try
            {
                var assembly = Assembly.Load(new AssemblyName("dotnet.host"));

                // Loader impl
                // The following code is doing:
                // var loaderContainer = new dotnet.host.LoaderContainer();
                // var cachedAssemblyLoader = new dotnet.host.CachedAssemblyLoader(_assemblyCache);
                // var libLoader = new dotnet.host.PathBasedAssemblyLoader(searchPaths);
                // loaderContainer.AddLoader(cachedAssemblyLoader);
                // loaderContainer.AddLoader(libLoader);
                // var bootstrapper = new dotnet.host.Bootstrapper(loaderContainer);
                // bootstrapper.Main(bootstrapperArgs);

                var loaderContainerType = assembly.GetType("dotnet.host.LoaderContainer");
                var cachedAssemblyLoaderType = assembly.GetType("dotnet.host.CachedAssemblyLoader");
                var pathBasedLoaderType = assembly.GetType("dotnet.host.PathBasedAssemblyLoader");

                var loaderContainer = Activator.CreateInstance(loaderContainerType);
                var cachedAssemblyLoader = Activator.CreateInstance(cachedAssemblyLoaderType, new object[] { _assemblyCache });
                var libLoader = Activator.CreateInstance(pathBasedLoaderType, new object[] { searchPaths });

                MethodInfo addLoaderMethodInfo = loaderContainerType.GetTypeInfo().GetDeclaredMethod("AddLoader");
                var disposable1 = (IDisposable)addLoaderMethodInfo.Invoke(loaderContainer, new[] { cachedAssemblyLoader });
                var disposable2 = (IDisposable)addLoaderMethodInfo.Invoke(loaderContainer, new[] { libLoader });

                var disposable = new CombinedDisposable(disposable1, disposable2);

                var loaderContainerLoadMethodInfo = loaderContainerType.GetTypeInfo().GetDeclaredMethod("Load");

                loader = (Func<string, Assembly>)loaderContainerLoadMethodInfo.CreateDelegate(typeof(Func<string, Assembly>), loaderContainer);

                var bootstrapperType = assembly.GetType("dotnet.host.Bootstrapper");
                var mainMethod = bootstrapperType.GetTypeInfo().GetDeclaredMethod("Main");
                var bootstrapper = Activator.CreateInstance(bootstrapperType, loaderContainer);

                try
                {
                    var bootstrapperArgs = new object[]
                    {
                        app.RemainingArguments.ToArray()
                    };

                    var task = (Task<int>)mainMethod.Invoke(bootstrapper, bootstrapperArgs);

                    return task.ContinueWith(async (t, state) =>
                    {
                        // Dispose the host
                        ((IDisposable)state).Dispose();

#if ASPNET50
                        AppDomain.CurrentDomain.AssemblyResolve -= handler;
#endif
                        return await t;
                    },
                    disposable).Unwrap();
                }
                catch
                {
                    // If we throw synchronously then dispose then rethtrow
                    disposable.Dispose();
                    throw;
                }
            }
            catch
            {
#if ASPNET50
                AppDomain.CurrentDomain.AssemblyResolve -= handler;
#endif
                throw;
            }
        }

        private static string[] ResolveSearchPaths(IEnumerable<string> libPaths, List<string> remainingArgs)
        {
            var searchPaths = new List<string>();

            // TODO: remove KRE_ env var
            var defaultLibPath = Environment.GetEnvironmentVariable("DOTNET_DEFAULT_LIB") ?? Environment.GetEnvironmentVariable("KRE_DEFAULT_LIB");

            if (!string.IsNullOrEmpty(defaultLibPath))
            {
                // Add the default lib folder if specified
                searchPaths.AddRange(ExpandSearchPath(defaultLibPath));
            }

            // Add the expanded search libs to the list of paths
            searchPaths.AddRange(libPaths.SelectMany(ExpandSearchPath));

            // If a .dll or .exe is specified then turn this into
            // --lib {path to dll/exe} [dll/exe name]
            if (remainingArgs.Any())
            {
                var application = remainingArgs[0];
                var extension = Path.GetExtension(application);

                if (!string.IsNullOrEmpty(extension) &&
                    extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Add the directory to the list of search paths
                    searchPaths.Add(Path.GetDirectoryName(Path.GetFullPath(application)));

                    // Modify the argument to be the dll/exe name
                    remainingArgs[0] = Path.GetFileNameWithoutExtension(application);
                }
            }

            return searchPaths.ToArray();
        }

        private static IEnumerable<string> ExpandSearchPath(string libPath)
        {
            // Expand ; separated arguments
            return libPath.Split(_libPathSeparator, StringSplitOptions.RemoveEmptyEntries)
                          .Select(Path.GetFullPath);
        }

        private static void ExtractAssemblyNeutralInterfaces(Assembly assembly, Func<Stream, Assembly> load)
        {
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (resourceName.StartsWith("AssemblyNeutral/") && 
                    resourceName.EndsWith(".dll"))
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(resourceName);

                    if (_assemblyCache.ContainsKey(assemblyName))
                    {
                        continue;
                    }

                    var neutralAssemblyStream = assembly.GetManifestResourceStream(resourceName);

                    var neutralAssembly = load(neutralAssemblyStream);

                    _assemblyCache[assemblyName] = neutralAssembly;
                }
            }
        }

        private static Assembly ResolveHostAssembly(Func<string, Assembly> loadFile, IList<string> searchPaths, string name)
        {
            foreach (var searchPath in searchPaths)
            {
                var path = Path.Combine(searchPath, name + ".dll");

                if (File.Exists(path))
                {
                    return loadFile(path);
                }
            }

            return null;
        }

        private static string GetVersion()
        {
            var assembly = typeof(RuntimeBootstrapper).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (assemblyInformationalVersionAttribute == null)
            {
                return assembly.GetName().Version.ToString();
            }
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }

        private class CombinedDisposable : IDisposable
        {
            private IDisposable _disposable1;
            private IDisposable _disposable2;

            public CombinedDisposable(IDisposable disposable1, IDisposable disposable2)
            {
                _disposable1 = disposable1;
                _disposable2 = disposable2;
            }

            public void Dispose()
            {
                if (_disposable2 != null)
                {
                    _disposable2.Dispose();
                    _disposable2 = null;
                }

                if (_disposable1 != null)
                {
                    _disposable1.Dispose();
                    _disposable1 = null;
                }
            }
        }
    }
}
