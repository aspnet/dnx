using Microsoft.CodeAnalysis.Text;
using System;

namespace Microsoft.Framework.DesignTimeHost.Models.IncomingMessages
{
    public class SourceTextChangeMessage
    {
        public string SourcePath { get; set; }

        public string NewText { get; set; }

        public int? Start { get; set; }
        public int? Length { get; set; }

        public int? StartLineNumber { get; set; }
        public int? StartCharacter { get; set; }
        public int? EndLineNumber { get; set; }
        public int? EndCharacter { get; set; }

        public bool isOffsetBased
        {   
            get {
                return Start != null && Length != null;
            }
        }

        public bool isLineBased
        {
            get {
                return StartLineNumber != null && StartCharacter != null && EndLineNumber != null && EndCharacter != null;
            }
        }
    }
}