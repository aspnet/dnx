// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Serialization;

namespace NuGet
{
    [XmlType("dependency")]
    public class ManifestDependency
    {
        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("version")]
        public string Version { get; set; }
    }
}