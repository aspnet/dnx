// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace NuGet
{
    [XmlType("reference")]
    public class ManifestReference : IEquatable<ManifestReference>
    {
        private static readonly char[] _referenceFileInvalidCharacters = Path.GetInvalidFileNameChars();

        [XmlAttribute("file")]
        public string File { get; set; }

        public bool Equals(ManifestReference other)
        {
            return other != null && String.Equals(File, other.File, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return File == null ? 0 : File.GetHashCode();
        }
    }
}
