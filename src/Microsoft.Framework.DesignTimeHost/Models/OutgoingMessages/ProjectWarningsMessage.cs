// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ProjectWarningsMessage : DiagnosticsMessage
    {
        private readonly IList<FileFormatWarning> _fileFormatWarnings;

        public ProjectWarningsMessage(IList<FileFormatWarning> fileFormatWarnings)
            : base(compilationMessages: fileFormatWarnings.Cast<ICompilationMessage>(), frameworkData: null)
        {
            _fileFormatWarnings = fileFormatWarnings;
        }

        public override JToken ConvertToJson(int protocolVersion)
        {
            if (protocolVersion < 2)
            {
                return null;
            }

            if (_fileFormatWarnings.Count == 0)
            {
                return null;
            }

            return base.ConvertToJson(protocolVersion);
        }

        public override bool Equals(object obj)
        {
            var other = obj as ProjectWarningsMessage;

            return other != null &&
                Enumerable.SequenceEqual(_fileFormatWarnings, other._fileFormatWarnings);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}