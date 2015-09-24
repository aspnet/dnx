// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet
{
    public static class SettingsExtensions
    {
        private const string ConfigSection = "config";

        public static string GetRepositoryPath(this ISettings settings)
        {
            string path = settings.GetValue(ConfigSection, "repositoryPath", isPath: true);
            if (!String.IsNullOrEmpty(path))
            {
                path = path.Replace('/', System.IO.Path.DirectorySeparatorChar);
            }
            return path;
        }

        public static string GetDecryptedValue(this ISettings settings, string section, string key, bool isPath = false)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(section));
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, nameof(key));
            }

            var encryptedString = settings.GetValue(section, key, isPath);
            if (encryptedString == null)
            {
                return null;
            }
            if (String.IsNullOrEmpty(encryptedString))
            {
                return String.Empty;
            }
            return EncryptionUtility.DecryptString(encryptedString);
        }

        /// <summary>
        /// Retrieves a config value for the specified key
        /// </summary>
        /// <param name="settings">The settings instance to retrieve </param>
        /// <param name="key">The key to look up</param>
        /// <param name="decrypt">Determines if the retrieved value needs to be decrypted.</param>
        /// <param name="isPath">Determines if the retrieved value is returned as a path.</param>
        /// <returns>Null if the key was not found, value from config otherwise.</returns>
        public static string GetConfigValue(this ISettings settings, string key, bool decrypt = false, bool isPath = false)
        {
            return decrypt ? 
                settings.GetDecryptedValue(ConfigSection, key, isPath) : 
                settings.GetValue(ConfigSection, key, isPath);
        }
    }
}
