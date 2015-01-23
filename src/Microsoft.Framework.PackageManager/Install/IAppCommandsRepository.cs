// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    internal interface IAppCommandsRepository
    {
        IFileSystem Root { get; }

        IFileSystem PackagesRoot { get; }

        IEnumerable<string> Commands { get; }

        NuGet.PackageInfo FindCommandOwner(string command);

        void Remove(string commandName);
    }
}