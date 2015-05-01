// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    internal static class NamedResourceReader
    {
        public static IDictionary<string, string> ReadNamedResources(JObject rawProject, string projectFilePath)
        {
            var namedResourceToken = rawProject["namedResource"];
            if (namedResourceToken == null)
            {
                return new Dictionary<string, string>();
            }

            if (namedResourceToken.Type != JTokenType.Object)
            {
                throw FileFormatException.Create("Value must be object.", namedResourceToken, projectFilePath);
            }

            var namedResources = new Dictionary<string, string>();

            foreach (var namedResource in namedResourceToken)
            {
                if (namedResource.Type != JTokenType.Property)
                {
                    throw FileFormatException.Create("Value must be property.", namedResource, projectFilePath);
                }

                var property = namedResource as JProperty;
                if (property.Value.Type != JTokenType.String)
                {
                    throw FileFormatException.Create("Value must be string.", property.Value, projectFilePath);
                }

                var resourcePath = property.Value.ToString();
                if (resourcePath.Contains("*"))
                {
                    throw FileFormatException.Create("Value cannot contain wildcards.", property.Value, projectFilePath);
                }

                var resourceFileFullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFilePath), resourcePath));

                if (namedResources.ContainsKey(property.Name))
                {
                    throw FileFormatException.Create(
                        string.Format("The named resource {0} already exists.", property.Name),
                        property,
                        projectFilePath);
                }
                namedResources.Add(
                    property.Name,
                    resourceFileFullPath);
            }

            return namedResources;
        }

        public static void ApplyNamedResources(IDictionary<string, string> namedResources, IDictionary<string, string> resources)
        {
            foreach (var namedResource in namedResources)
            {
                // The named resources dictionary is like the project file
                // key = name, value = path to resource
                if (resources.ContainsKey(namedResource.Value))
                {
                    resources[namedResource.Value] = namedResource.Key;
                }
                else
                {
                    resources.Add(namedResource.Value, namedResource.Key);
                }
            }
        }
    }
}