// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace dotnet.host
{
    internal class FrameworkNameUtility
    {
        internal static FrameworkName ParseFrameworkName(string frameworkName)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException("frameworkName");
            }

            // {Identifier}{Version}-{Profile}

            // Split the framework name into 3 parts, identifier, version and profile.
            string identifierPart = null;
            string versionPart = null;

            string[] parts = frameworkName.Split('-');

            if (parts.Length > 2)
            {
                throw new ArgumentException("frameworkName");
            }

            string frameworkNameAndVersion = parts.Length > 0 ? parts[0].Trim() : null;
            string profilePart = parts.Length > 1 ? parts[1].Trim() : null;

            if (String.IsNullOrEmpty(frameworkNameAndVersion))
            {
                throw new ArgumentException("frameworkName");
            }

            // If we find a version then we try to split the framework name into 2 parts
            var versionMatch = Regex.Match(frameworkNameAndVersion, @"\d+");

            if (versionMatch.Success)
            {
                identifierPart = frameworkNameAndVersion.Substring(0, versionMatch.Index).Trim();
                versionPart = frameworkNameAndVersion.Substring(versionMatch.Index).Trim();
            }
            else
            {
                // Otherwise we take the whole name as an identifier
                identifierPart = frameworkNameAndVersion.Trim();
            }

            if (!String.IsNullOrEmpty(identifierPart))
            {
                if (identifierPart.Equals("aspnet", StringComparison.OrdinalIgnoreCase))
                {
                    identifierPart = "Asp.Net";
                }
                else if (identifierPart.Equals("aspnetcore", StringComparison.OrdinalIgnoreCase))
                {
                    identifierPart = "Asp.NetCore";
                }
                else if (identifierPart.Equals("k", StringComparison.OrdinalIgnoreCase))
                {
                    identifierPart = "Asp.NetCore";
                }
            }

            Version version = null;
            // We support version formats that are integers (40 becomes 4.0)
            int versionNumber;
            if (Int32.TryParse(versionPart, out versionNumber))
            {
                // Remove the extra numbers
                if (versionPart.Length > 4)
                {
                    versionPart = versionPart.Substring(0, 4);
                }

                // Make sure it has at least 2 digits so it parses as a valid version
                versionPart = versionPart.PadRight(2, '0');
                versionPart = String.Join(".", versionPart.ToCharArray());
            }

            // If we can't parse the version then use the default
            if (!Version.TryParse(versionPart, out version))
            {
                // We failed to parse the version string once more. So we need to decide if this is unsupported or if we use the default version.
                // This framework is unsupported if:
                // 1. The identifier part of the framework name is null.
                // 2. The version part is not null.
                if (String.IsNullOrEmpty(identifierPart) || !String.IsNullOrEmpty(versionPart))
                {
                    return null;
                }

                version = new Version(0, 0);
            }

            if (String.IsNullOrEmpty(identifierPart))
            {
                identifierPart = ".NETFramework";
            }

            return new FrameworkName(identifierPart, version, profilePart);
        }
    }
}
