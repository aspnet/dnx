// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Framework.Runtime
{
    internal static class CreateCSharpManifestResourceName
    {
        // Original source: https://raw.githubusercontent.com/Microsoft/msbuild/82177a50da735cc0443ac10fa490d69368403d71/src/XMakeTasks/CreateCSharpManifestResourceName.cs

        public static string CreateManifestName(string fileName, string rootNamespace)
        {
            StringBuilder name = new StringBuilder();

            // Differences from the msbuild task:
            // - we do not include the name of the first class (if any) for binary resources or source code
            // - culture info is ignored

            if (rootNamespace != null && rootNamespace.Length > 0)
            {
                name.Append(rootNamespace).Append(".");
            }

            // Replace spaces in the directory name with underscores. Needed for compatibility with Everett.
            // Note that spaces in the file name itself are preserved.
            string path = MakeValidEverettIdentifier(Path.GetDirectoryName(fileName));

            // This is different from the msbuild task: we always append extensions because otherwise,
            // the emitted resource doesn't have an extension and it is not the same as in the classic
            // C# assembly
            if (ResxResourceProvider.IsResxResourceFile(fileName))
            {
                name.Append(Path.Combine(path, Path.GetFileNameWithoutExtension(fileName)));
                name.Append(".resources");
                name.Replace(Path.DirectorySeparatorChar, '.');
                name.Replace(Path.AltDirectorySeparatorChar, '.');
            }
            else
            {
                name.Append(Path.Combine(path, Path.GetFileName(fileName)));
                name.Replace(Path.DirectorySeparatorChar, '.');
                name.Replace(Path.AltDirectorySeparatorChar, '.');
            }

            return name.ToString();
        }

        // The code below the same is same as here: https://raw.githubusercontent.com/Microsoft/msbuild/41b137cd8805079af7792995e044521d62fcb005/src/XMakeTasks/CreateManifestResourceName.cs

        /// <summary>
        /// This method is provided for compatibility with Everett which used to convert parts of resource names into
        /// valid identifiers
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string MakeValidEverettIdentifier(string name)
        {
            StringBuilder everettId = new StringBuilder(name.Length);

            // split the name into folder names
            string[] subNames = name.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

            // convert every folder name
            everettId.Append(MakeValidEverettFolderIdentifier(subNames[0]));

            for (int i = 1; i < subNames.Length; i++)
            {
                everettId.Append('.');
                everettId.Append(MakeValidEverettFolderIdentifier(subNames[i]));
            }

            return everettId.ToString();
        }

        /// <summary>
        /// Make a folder name into an Everett-compatible identifier
        /// </summary>
        private static string MakeValidEverettFolderIdentifier(string name)
        {
            // give string length to avoid reallocations; +1 since the resulting string may be one char longer than the
            // original - if the name is a single underscore we add another underscore to it
            StringBuilder everettId = new StringBuilder(name.Length + 1);

            // split folder name into subnames separated by '.', if any
            string[] subNames = name.Split(new char[] { '.' });

            // convert each subname separately
            everettId.Append(MakeValidEverettSubFolderIdentifier(subNames[0]));

            for (int i = 1; i < subNames.Length; i++)
            {
                everettId.Append('.');
                everettId.Append(MakeValidEverettSubFolderIdentifier(subNames[i]));
            }

            // folder name cannot be a single underscore - add another underscore to it
            if (everettId.ToString() == "_")
                everettId.Append('_');

            return everettId.ToString();
        }
        /// <summary>
        /// Make a folder subname into an Everett-compatible identifier 
        /// </summary>
        private static string MakeValidEverettSubFolderIdentifier(string subName)
        {
            if (subName.Length == 0)
                return subName;

            // give string length to avoid reallocations; +1 since the resulting string may be one char longer than the
            // original - if the first character is an invalid first identifier character but a valid subsequent one,
            // we prepend an underscore to it.
            StringBuilder everettId = new StringBuilder(subName.Length + 1);

            // the first character has stronger restrictions than the rest
            if (!IsValidEverettIdFirstChar(subName[0]))
            {
                // if the first character is not even a valid subsequent character, replace it with an underscore
                if (!IsValidEverettIdChar(subName[0]))
                {
                    everettId.Append('_');
                }
                // if it is a valid subsequent character, prepend an underscore to it
                else
                {
                    everettId.Append('_');
                    everettId.Append(subName[0]);
                }
            }
            else
            {
                everettId.Append(subName[0]);
            }

            // process the rest of the subname
            for (int i = 1; i < subName.Length; i++)
            {
                if (!IsValidEverettIdChar(subName[i]))
                {
                    everettId.Append('_');
                }
                else
                {
                    everettId.Append(subName[i]);
                }
            }

            return everettId.ToString();
        }

        /// <summary>
        /// Is the character a valid first Everett identifier character?
        /// </summary>
        private static bool IsValidEverettIdFirstChar(char c)
        {
            return
                char.IsLetter(c) ||
                CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.ConnectorPunctuation;
        }

        /// <summary>
        /// Is the character a valid Everett identifier character?
        /// </summary>
        private static bool IsValidEverettIdChar(char c)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);

            return
                char.IsLetterOrDigit(c) ||
                cat == UnicodeCategory.ConnectorPunctuation ||
                cat == UnicodeCategory.NonSpacingMark ||
                cat == UnicodeCategory.SpacingCombiningMark ||
                cat == UnicodeCategory.EnclosingMark;
        }
    }
}