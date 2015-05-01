// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Serialization;

namespace NuGet
{
    [XmlType("frameworkAssembly")]
    public class ManifestFrameworkAssembly
    {
        [XmlAttribute("assemblyName")]
        public string AssemblyName { get; set; }

        [XmlAttribute("targetFramework")]
        public string TargetFramework { get; set; }
    }
}
