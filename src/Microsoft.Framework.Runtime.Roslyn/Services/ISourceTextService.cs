using System;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Framework.Runtime.Roslyn.Services
{
    public interface ISourceTextService
    {
        SourceText GetSourceText(string sourcePath);

        void RecordTextChange(string sourcePath, TextSpan span, string newText);

        void RecordTextChange(string sourcePath, LinePositionSpan span, string newText);
    }
}