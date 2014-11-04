using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Runtime.Roslyn.Services;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class SourceTextWithChanges
    {
        private readonly object _lock = new object();

        private readonly List<TextChange> _changes = new List<TextChange>();

        private SourceText _value;

        public SourceTextWithChanges(string sourcePath)
        {
            using (var stream = File.OpenRead(sourcePath))
            {
                _value = SourceText.From(stream, encoding: Encoding.UTF8);
            };
        }

        public int Version { get; private set; }

        public SourceText Value
        {
            get
            {
                lock (_lock)
                {
                    // the changes might overlap and cannot 
                    // always be applied in one go
                    foreach (var change in _changes)
                    {
                        _value = _value.WithChanges(new TextChange[] { change });
                    }
                    _changes.Clear();
                    return _value;
                }
            }
        }

        public void Record(TextChange change)
        {
            lock (_lock)
            {
                _changes.Add(change);
                Version += 1;
            }
        }
    }
}