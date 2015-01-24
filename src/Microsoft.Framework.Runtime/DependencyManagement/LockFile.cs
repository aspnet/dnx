// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.DependencyManagement
{
    public class LockFile
    {
        public bool Islocked { get; set; }
        public IList<LockFileLibrary> Libraries { get; set; } = new List<LockFileLibrary>();
    }
}