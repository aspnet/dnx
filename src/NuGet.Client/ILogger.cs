// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Client
{
    public interface ILogger
    {
        void WriteVerbose(string message);
        void WriteInformation(string message);
        void WriteError(string message);
        void WriteQuiet(string message);
    }
}