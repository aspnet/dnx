using Microsoft.Framework.Runtime.Roslyn.Services;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class SourceTextVersionChangeDependency : ICacheDependency
    {
        private SourceTextWithChanges _sourceText;

        private int _version;

        public SourceTextVersionChangeDependency(SourceTextWithChanges sourceText)
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
}