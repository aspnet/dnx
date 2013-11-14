using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NuGet.Resources;

namespace NuGet
{
    [XmlType("file")]
    public class ManifestFile
    {
        private static readonly char[] _invalidTargetChars = Path.GetInvalidFileNameChars().Except(new[] { '\\', '/', '.' }).ToArray();
        private static readonly char[] _invalidSourceCharacters = Path.GetInvalidPathChars();

        [XmlAttribute("src")]
        public string Source { get; set; }

        [XmlAttribute("target")]
        public string Target { get; set; }

        [XmlAttribute("exclude")]
        public string Exclude { get; set; }
    }
}