// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.DesignTimeHost.Models.IncomingMessages
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

        public bool IsOffsetBased
        {   
            get 
            {
                return Start != null && Length != null;
            }
        }

        public bool IsLineBased
        {
            get 
            {
                return StartLineNumber != null && StartCharacter != null && EndLineNumber != null && EndCharacter != null;
            }
        }
    }
}