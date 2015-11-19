// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Dnx.Tooling.Packages.Workers;

namespace Microsoft.Dnx.Tooling.Packages
{
    public abstract class PackagesCommand<TOptions>
    {
        public PackagesCommand(TOptions options)
        {
            Options = options;
        }

        public TOptions Options { get; private set; }
        public string LocalPackages { get; set; }
        public Reports Reports { get; set; }

        protected RepositoryTransmitRecord FillOut(RepositoryTransmitRecord record)
        {
            if (record == null)
            {
                record = new RepositoryTransmitRecord();
            }

            if (record.Push == null)
            {
                record.Push = new Dictionary<string, int>();
            }

            if (record.Pull == null)
            {
                record.Pull = new Dictionary<string, int>();
            }

            return record;
        }

        protected RepositoryChangeRecord FillOut(RepositoryChangeRecord record)
        {
            if (record == null)
            {
                record = new RepositoryChangeRecord();
            }

            if (record.Next == 0)
            {
                record.Next = 1;
            }

            if (record.Add == null)
            {
                record.Add = new List<string>();
            }

            if (record.Remove == null)
            {
                record.Remove = new List<string>();
            }

            return record;
        }
    }
}