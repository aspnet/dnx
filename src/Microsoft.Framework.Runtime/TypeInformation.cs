// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime
{
    public class TypeInformation
    {
        private readonly Tuple<string, string> _tuple;

        public TypeInformation(string assemblyName, string typeName)
        {
            _tuple = Tuple.Create(assemblyName, typeName);
        }

        public string AssemblyName
        {
            get
            {
                return _tuple.Item1;
            }
        }

        public string TypeName
        {
            get
            {
                return _tuple.Item2;
            }
        }

        public override int GetHashCode()
        {
            return _tuple.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var li = obj as TypeInformation;

            return li != null && li._tuple.Equals(_tuple);
        }

        public TInstance CreateInstance<TInstance>(IAssemblyLoadContext loadContext, IServiceProvider serviceProvider)
        {
            var assembly = loadContext.Load(AssemblyName);

            var type = assembly.GetType(TypeName);

            return (TInstance)ActivatorUtilities.CreateInstance(serviceProvider, type);
        }
    }
}