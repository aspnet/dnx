// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public sealed class FileFormatWarning
    {
        public FileFormatWarning(string message, string projectFilePath, JToken token)
        {
            Message = message;
            Path = projectFilePath;

            var lineInfo = (IJsonLineInfo)token;

            Column = lineInfo.LinePosition;
            Line = lineInfo.LineNumber;
        }

        public string Message { get; }

        public string Path { get; }

        public int Line { get; }

        public int Column { get; }
    }
}