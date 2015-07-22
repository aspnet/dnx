// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Runtime;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost
{
    public interface IPlugin
    {
        bool ProcessMessage(JObject data, IAssemblyLoadContext assemblyLoadContext);

        int Protocol { get; set; }
    }
}