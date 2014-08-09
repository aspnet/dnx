using Microsoft.Framework.PackageManager.Feeds.Workers;
using System;
using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager.Feeds
{
    public abstract class FeedCommand<TOptions>
    {
        public FeedCommand(TOptions options)
        {
            Options = options;
        }

        public TOptions Options { get; private set; }
        public string LocalPackages { get; set; }
        public IReport Report { get; set; }

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