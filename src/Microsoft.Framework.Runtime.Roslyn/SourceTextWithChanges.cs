using System;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Runtime.Roslyn.Services;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class SourceTextWithChanges
    {
        public int Version { get; private set; }

        private SourceText _value;

        private Lazy<List<TextChange>> _changes = new Lazy<List<TextChange>>(() => new List<TextChange>());

        public SourceTextWithChanges(string sourcePath)
        {
            using (var stream = File.OpenRead(sourcePath))
            {
                _value = SourceText.From(stream, encoding: Encoding.UTF8);
            };
        }

        public void Record(TextChange change)
        {
            _changes.Value.Add(change);
            Version += 1;
        }

        public SourceText Value {
            get {
                if (_changes.IsValueCreated)
                {
                    // the changes might overlap and cannot 
                    // always be applied in one go
                    foreach(var change in _changes.Value)
                    {
                        _value = _value.WithChanges(new TextChange[] { change });
                    }
                    _changes.Value.Clear();
                }
                return _value;
            }
        }
    }
}