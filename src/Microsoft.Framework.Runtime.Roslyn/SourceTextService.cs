using System;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Runtime.Roslyn.Services;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class SourceTextService : ISourceTextService
    {
        private readonly ICache _cache;

        public SourceTextService(ICache cache)
        {
            _cache = cache;
        }

        public SourceText GetSourceText(string sourcePath)
        {
            return _cache.Get<SourceText>("STS_" + sourcePath, ctx =>
            {
                var sourceText = GetOrCreateSourceText(sourcePath);
                // cache: invalidate whenever a change is being recorded
                ctx.Monitor(new SourceTextVersionChangeDependency(sourceText));
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
            return _cache.Get<SourceTextWithChanges>("STWC_" + sourcePath, (ctx, old) =>
            {
                // cache: invalidate whenever file changes on disk
                ctx.Monitor(new FileWriteTimeCacheDependency(sourcePath));
                return new SourceTextWithChanges(sourcePath);
            });
        }
    }
}