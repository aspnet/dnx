// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class TestAssembly : Assembly
    {
        private readonly IReadOnlyDictionary<string, Type> _typeNameLookups;

        public TestAssembly()
            : this(new Dictionary<string, Type>())
        {
        }

        public TestAssembly(IReadOnlyDictionary<string, Type> typeNameLookups)
        {
            _typeNameLookups = typeNameLookups;
        }

        public override Type GetType(string name)
        {
            Type type;
            _typeNameLookups.TryGetValue(name, out type);

            return type;
        }
    }
}