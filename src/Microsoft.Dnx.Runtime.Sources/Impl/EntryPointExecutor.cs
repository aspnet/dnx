// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;

namespace Microsoft.Dnx.Runtime.Common
{
    internal static class EntryPointExecutor
    {
        public static Task<int> Execute(Assembly assembly, string[] args, IServiceProvider serviceProvider)
        {
            object instance;
            MethodInfo entryPoint;

            if (!TryGetEntryPoint(assembly, serviceProvider, out instance, out entryPoint))
            {
                return Task.FromResult(1);
            }

            object result = null;
            var parameters = entryPoint.GetParameters();

            try
            {
                if (parameters.Length == 0)
                {
                    result = entryPoint.Invoke(instance, null);
                }
                else if (parameters.Length == 1)
                {
                    result = entryPoint.Invoke(instance, new object[] { args });
                }
            }
            catch (Exception ex)
            {
                if (ex is TargetInvocationException)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }

                throw;
            }

            if (result is int)
            {
                return Task.FromResult((int)result);
            }

            if (result is Task<int>)
            {
                return (Task<int>)result;
            }

            if (result is Task)
            {
                return ((Task)result).ContinueWith(t =>
                {
                    return 0;
                });
            }

            return Task.FromResult(0);
        }

        private static bool TryGetEntryPoint(Assembly assembly, IServiceProvider serviceProvider, out object instance, out MethodInfo entryPoint)
        {
            string name = assembly.GetName().Name;

            instance = null;

            // Add support for console apps
            // This allows us to boot any existing console application
            // under the runtime
            entryPoint = GetEntryPoint(assembly);
            if (entryPoint != null)
            {
                return true;
            }

            var programType = assembly.GetType("Program") ?? assembly.GetType(name + ".Program");

            if (programType == null)
            {
                var programTypeInfo = assembly.DefinedTypes.FirstOrDefault(t => t.Name == "Program");

                if (programTypeInfo == null)
                {
                                        
                    System.Console.WriteLine("'{0}' does not contain a 'Program' type suitable for an entry point", name);
                    return false;
                }

                programType = programTypeInfo.AsType();
            }

            entryPoint = programType.GetTypeInfo().GetDeclaredMethods("Main").FirstOrDefault();

            if (entryPoint == null)
            {
                System.Console.WriteLine("'{0}' does not contain a 'Main' method suitable for an entry point", name);
                return false;
            }

            instance = programType.GetTypeInfo().IsAbstract ? null : ActivatorUtilities.CreateInstance(serviceProvider, programType);
            return true;
        }

        private static MethodInfo GetEntryPoint(Assembly assembly)
        {
#if DNX451
            return assembly.EntryPoint;
#else
            // Temporary until https://github.com/dotnet/corefx/issues/3336 is fully merged and built
            return assembly.GetType().GetRuntimeProperty("EntryPoint").GetValue(assembly) as MethodInfo;
#endif
        }
    }
}
