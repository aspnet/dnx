using System;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Runtime.Roslyn.Services;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using System.Threading;

namespace Microsoft.Framework.Runtime.Roslyn
{
    class VersionCacheDependency : ICacheDependency
    {
        private SourceTextWithChanges _sourceText;

        private int _version;

        internal VersionCacheDependency(SourceTextWithChanges sourceText)
        {
            _sourceText = sourceText;
            _version = sourceText.Version;
        }

        public bool HasChanged {
            get {
                return _sourceText.Version != _version;
            }
        }
    }

    class SourceTextWithChanges
    {
        public int Version { get; private set; }

        private SourceText _value;

        private Lazy<List<TextChange>> _changes = new Lazy<List<TextChange>>(() => new List<TextChange>());

        internal SourceTextWithChanges(string sourcePath)
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

    public class SourceTextService : ISourceTextService
    {

        private ICache _cache;

        public SourceTextService(ICache cache)
        {
            _cache = cache;
        }

        public SourceText GetSourceText(string sourcePath)
        {
            return _cache.Get<SourceText>("STS_" + sourcePath, ctx => {
                var sourceText = GetOrCreateSourceText(sourcePath);
                // cache: invalidate whenever a change is being recorded
                ctx.Monitor(new VersionCacheDependency(sourceText));
                return sourceText.Value;
            });
        }

        public void RecordTextChange(string sourcePath, LinePositionSpan span, string newText)
        {
            var sourceText = GetOrCreateSourceText(sourcePath);
            var startOffset = sourceText.Value.Lines[span.Start.Line].Start + span.Start.Character;
            var endOffset = sourceText.Value.Lines[span.End.Line].Start + span.End.Character;
            sourceText.Record(new TextChange(new TextSpan(startOffset, endOffset - startOffset), newText));
        }

        public void RecordTextChange(string sourcePath, TextSpan span, string newText)
        {
            GetOrCreateSourceText(sourcePath).Record(new TextChange(span, newText));
        }

        private SourceTextWithChanges GetOrCreateSourceText(string sourcePath)
        {
            return _cache.Get<SourceTextWithChanges>("STWC_" + sourcePath, (ctx, old) => {
                // cache: invalidate whenever file changes on disk
                ctx.Monitor(new FileWriteTimeCacheDependency(sourcePath));
                var ret = new SourceTextWithChanges(sourcePath);
                return ret;
            });
        }
    }
}