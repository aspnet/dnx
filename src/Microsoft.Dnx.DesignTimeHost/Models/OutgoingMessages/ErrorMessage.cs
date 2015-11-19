// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages
{
    public class ErrorMessage
    {
        public string Message { get; set; }
        public string Path { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public override bool Equals(object obj)
        {
            var errorMessage = obj as ErrorMessage;

            return errorMessage != null && 
                   string.Equals(Message, errorMessage.Message, StringComparison.Ordinal) &&
                   string.Equals(Path, errorMessage.Path, StringComparison.OrdinalIgnoreCase) &&
                   Line == errorMessage.Line &&
                   Column == errorMessage.Column;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
