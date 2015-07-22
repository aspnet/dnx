// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;

namespace NuGet
{
    internal static class UriUtility
    {
        /// <summary>
        /// Converts a uri to a path. Only used for local paths.
        /// </summary>
        internal static string GetPath(Uri uri)
        {
            return GetPath(uri, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Converts a uri to a path with given path separator. Only used for local paths.
        /// </summary>
        internal static string GetPath(Uri uri, char separator)
        {
            string path = uri.OriginalString;
            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            // Bug 483: We need the unescaped uri string to ensure that all characters are valid for a path.
            // Change the direction of the slashes to match the given separator.
            return Uri.UnescapeDataString(path.Replace('/', separator));
        }

        // Bug 2379: SettingsCredentialProvider does not work
        private static Uri CreateODataAgnosticUri(string uri)
        {
            if (uri.EndsWith("$metadata", StringComparison.OrdinalIgnoreCase))
            {
                uri = uri.Substring(0, uri.Length - 9).TrimEnd('/');
            }
            return new Uri(uri);
        }

        /// <summary>
        /// Determines if the scheme, server and path of two Uris are identical.
        /// </summary>
        public static bool UriEquals(Uri uri1, Uri uri2)
        {
            uri1 = CreateODataAgnosticUri(uri1.OriginalString.TrimEnd('/'));
            uri2 = CreateODataAgnosticUri(uri2.OriginalString.TrimEnd('/'));

            return Uri.Compare(uri1, uri2, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
