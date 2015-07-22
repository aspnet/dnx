// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet
{
    public class PackageSourceProvider : IPackageSourceProvider
    {
        private const string PackageSourcesSectionName = "packageSources";
        private const string DisabledPackageSourcesSectionName = "disabledPackageSources";
        private const string CredentialsSectionName = "packageSourceCredentials";
        private const string UsernameToken = "Username";
        private const string PasswordToken = "Password";
        private const string ClearTextPasswordToken = "ClearTextPassword";
        private readonly ISettings _settingsManager;
        private readonly IEnumerable<PackageSource> _providerDefaultSources;
        private readonly IDictionary<PackageSource, PackageSource> _migratePackageSources;
        private readonly IEnumerable<PackageSource> _configurationDefaultSources;

        public PackageSourceProvider(ISettings settingsManager)
            : this(settingsManager, providerDefaultSources: null)
        {
        }

        /// <summary>
        /// Creates a new PackageSourceProvider instance.
        /// </summary>
        /// <param name="settingsManager">Specifies the settings file to use to read package sources.</param>
        /// <param name="providerDefaultSources">Specifies the default sources to be used as per the PackageSourceProvider. These are always loaded
        /// Default Feeds from PackageSourceProvider are generally the feeds from the NuGet Client like the NuGetOfficialFeed from the Visual Studio client for NuGet</param>
        public PackageSourceProvider(ISettings settingsManager, IEnumerable<PackageSource> providerDefaultSources)
            : this(settingsManager, providerDefaultSources, migratePackageSources: null)
        {
        }

        public PackageSourceProvider(
            ISettings settingsManager,
            IEnumerable<PackageSource> providerDefaultSources,
            IDictionary<PackageSource, PackageSource> migratePackageSources)
            : this(settingsManager, providerDefaultSources, migratePackageSources, ConfigurationDefaults.Instance.DefaultPackageSources)
        {

        }

        internal PackageSourceProvider(
            ISettings settingsManager,
            IEnumerable<PackageSource> providerDefaultSources,
            IDictionary<PackageSource, PackageSource> migratePackageSources,
            IEnumerable<PackageSource> configurationDefaultSources)
        {
            if (settingsManager == null)
            {
                throw new ArgumentNullException(nameof(settingsManager));
            }
            _settingsManager = settingsManager;
            _providerDefaultSources = providerDefaultSources ?? Enumerable.Empty<PackageSource>();
            _migratePackageSources = migratePackageSources;
            _configurationDefaultSources = configurationDefaultSources ?? Enumerable.Empty<PackageSource>();
        }

        /// <summary>
        /// Returns PackageSources if specified in the config file. Else returns the default sources specified in the constructor.
        /// If no default values were specified, returns an empty sequence.
        /// </summary>
        public IEnumerable<PackageSource> LoadPackageSources()
        {
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var settingsValue = new List<SettingValue>();            
            IList<SettingValue> values = _settingsManager.GetSettingValues(PackageSourcesSectionName, isPath: false);
            var machineWideSourcesCount = 0;
            
            if (!values.IsEmpty())
            {
                var machineWideSources = new List<SettingValue>();

                // remove duplicate sources. Pick the one with the highest priority.
                // note that Reverse() is needed because items in 'values' is in
                // ascending priority order.
                foreach (var settingValue in values.Reverse())
                {
                    if (!sources.Contains(settingValue.Key))
                    {
                        if (settingValue.IsMachineWide)
                        {
                            machineWideSources.Add(settingValue);
                        }
                        else
                        {
                            settingsValue.Add(settingValue); 
                        }

                        sources.Add(settingValue.Key);
                    }
                }

                // Reverse the the list to be backward compatible
                settingsValue.Reverse();
                machineWideSourcesCount = machineWideSources.Count;

                // Add machine wide sources at the end
                settingsValue.AddRange(machineWideSources);
            }

            var loadedPackageSources = new List<PackageSource>();
            if (!settingsValue.IsEmpty())
            {
                // put disabled package source names into the hash set

                IEnumerable<KeyValuePair<string, string>> disabledSourcesValues = _settingsManager.GetValues(DisabledPackageSourcesSectionName) ??
                                                                                  Enumerable.Empty<KeyValuePair<string, string>>();
                var disabledSources = new HashSet<string>(disabledSourcesValues.Select(s => s.Key), StringComparer.CurrentCultureIgnoreCase);
                loadedPackageSources = settingsValue.
                                           Select(p =>
                                           {
                                               string name = p.Key;
                                               string src = p.Value;
                                               PackageSourceCredential creds = ReadCredential(name);

                                               return new PackageSource(src, name, isEnabled: !disabledSources.Contains(name), isOfficial: false, origin: p.Origin)
                                               {
                                                   UserName = creds != null ? creds.Username : null,
                                                   Password = creds != null ? creds.Password : null,
                                                   IsPasswordClearText = creds != null && creds.IsPasswordClearText,
                                                   IsMachineWide = p.IsMachineWide
                                               };

                                           }).ToList();

                if (_migratePackageSources != null)
                {
                    MigrateSources(loadedPackageSources);
                }
            }

            SetDefaultPackageSources(loadedPackageSources, machineWideSourcesCount);

            return loadedPackageSources;
        }

        private PackageSourceCredential ReadCredential(string sourceName)
        {
            var values = _settingsManager.GetNestedValues(CredentialsSectionName, sourceName);
            if (!values.IsEmpty())
            {
                string userName = values.FirstOrDefault(k => k.Key.Equals(UsernameToken, StringComparison.OrdinalIgnoreCase)).Value;

                if (!String.IsNullOrEmpty(userName))
                {
                    string encryptedPassword = values.FirstOrDefault(k => k.Key.Equals(PasswordToken, StringComparison.OrdinalIgnoreCase)).Value;
                    if (!String.IsNullOrEmpty(encryptedPassword))
                    {
                        return new PackageSourceCredential(userName, EncryptionUtility.DecryptString(encryptedPassword), isPasswordClearText: false);
                    }

                    string clearTextPassword = values.FirstOrDefault(k => k.Key.Equals(ClearTextPasswordToken, StringComparison.Ordinal)).Value;
                    if (!String.IsNullOrEmpty(clearTextPassword))
                    {
                        return new PackageSourceCredential(userName, clearTextPassword, isPasswordClearText: true);
                    }
                }
            }
            return null;
        }

        private void MigrateSources(List<PackageSource> loadedPackageSources)
        {
            bool hasChanges = false;
            List<PackageSource> packageSourcesToBeRemoved = new List<PackageSource>();

            // doing migration
            for (int i = 0; i < loadedPackageSources.Count; i++)
            {
                PackageSource ps = loadedPackageSources[i];
                PackageSource targetPackageSource;
                if (_migratePackageSources.TryGetValue(ps, out targetPackageSource))
                {
                    if (loadedPackageSources.Any(p => p.Equals(targetPackageSource)))
                    {
                        packageSourcesToBeRemoved.Add(loadedPackageSources[i]);
                    }
                    else
                    {
                        loadedPackageSources[i] = targetPackageSource.Clone();
                        // make sure we preserve the IsEnabled property when migrating package sources
                        loadedPackageSources[i].IsEnabled = ps.IsEnabled;
                    }
                    hasChanges = true;
                }
            }

            foreach (PackageSource packageSource in packageSourcesToBeRemoved)
            {
                loadedPackageSources.Remove(packageSource);
            }

            if (hasChanges)
            {
                SavePackageSources(loadedPackageSources);
            }
        }

        private void SetDefaultPackageSources(List<PackageSource> loadedPackageSources, int machineWideSourcesCount)
        {
            // There are 4 different cases to consider for default package sources
            // Case 1. Default Package Source is already present matching both feed source and the feed name
            // Case 2. Default Package Source is already present matching feed source but with a different feed name. DO NOTHING
            // Case 3. Default Package Source is not present, but there is another feed source with the same feed name. Override that feed entirely
            // Case 4. Default Package Source is not present, simply, add it

            IEnumerable<PackageSource> allDefaultPackageSources = _configurationDefaultSources;

            if (allDefaultPackageSources.IsEmpty<PackageSource>())
            {
                // Update provider default sources and use provider default sources since _configurationDefaultSources is empty
                UpdateProviderDefaultSources(loadedPackageSources);
                allDefaultPackageSources = _providerDefaultSources;
            }

            var defaultPackageSourcesToBeAdded = new List<PackageSource>();
            foreach (PackageSource packageSource in allDefaultPackageSources)
            {
                int sourceMatchingIndex = loadedPackageSources.FindIndex(p => p.Source.Equals(packageSource.Source, StringComparison.OrdinalIgnoreCase));
                if (sourceMatchingIndex != -1)
                {
                    if (loadedPackageSources[sourceMatchingIndex].Name.Equals(packageSource.Name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        // Case 1: Both the feed name and source matches. DO NOTHING except set IsOfficial to true
                        loadedPackageSources[sourceMatchingIndex].IsOfficial = true;
                    }
                    else
                    {
                        // Case 2: Only feed source matches but name is different. DO NOTHING
                    }
                }
                else
                {
                    int nameMatchingIndex = loadedPackageSources.FindIndex(p => p.Name.Equals(packageSource.Name, StringComparison.CurrentCultureIgnoreCase));
                    if (nameMatchingIndex != -1)
                    {
                        // Case 3: Only feed name matches but source is different. Override it entirely
                        loadedPackageSources[nameMatchingIndex] = packageSource;
                    }
                    else
                    {
                        // Case 4: Default package source is not present. Add it to the temp list. Later, the temp listed is inserted above the machine wide sources
                        defaultPackageSourcesToBeAdded.Add(packageSource);
                    }
                }
            }
            loadedPackageSources.InsertRange(loadedPackageSources.Count - machineWideSourcesCount, defaultPackageSourcesToBeAdded);
        }

        private void UpdateProviderDefaultSources(List<PackageSource> loadedSources)
        {
            // If there are NO other non-machine wide sources, providerDefaultSource should be enabled
            bool areProviderDefaultSourcesEnabled = loadedSources.Count == 0 || loadedSources.Where(p => !p.IsMachineWide).Count() == 0;

            foreach (PackageSource packageSource in _providerDefaultSources)
            {
                packageSource.IsEnabled = areProviderDefaultSourcesEnabled;
                packageSource.IsOfficial = true;
            }
        }

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            // clear the old values
            _settingsManager.DeleteSection(PackageSourcesSectionName);

            // and write the new ones
            _settingsManager.SetValues(
                PackageSourcesSectionName,
                sources.Where(p => !p.IsMachineWide)
                    .Select(p => new KeyValuePair<string, string>(p.Name, p.Source))
                    .ToList());

            // overwrite new values for the <disabledPackageSources> section
            _settingsManager.DeleteSection(DisabledPackageSourcesSectionName);

            _settingsManager.SetValues(
                DisabledPackageSourcesSectionName,
                sources.Where(p => !p.IsEnabled).Select(p => new KeyValuePair<string, string>(p.Name, "true")).ToList());

            // Overwrite the <packageSourceCredentials> section
            _settingsManager.DeleteSection(CredentialsSectionName);

            var sourceWithCredentials = sources.Where(s => !String.IsNullOrEmpty(s.UserName) && !String.IsNullOrEmpty(s.Password));
            foreach (var source in sourceWithCredentials)
            {
                _settingsManager.SetNestedValues(CredentialsSectionName, source.Name, new[] {
                    new KeyValuePair<string, string>(UsernameToken, source.UserName),
                    ReadPasswordValues(source)
                });
            }
        }

        private static KeyValuePair<string, string> ReadPasswordValues(PackageSource source)
        {
            string passwordToken = source.IsPasswordClearText ? ClearTextPasswordToken : PasswordToken;
            string passwordValue = source.IsPasswordClearText ? source.Password : EncryptionUtility.EncryptString(source.Password);

            return new KeyValuePair<string, string>(passwordToken, passwordValue);
        }

        public void DisablePackageSource(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _settingsManager.SetValue(DisabledPackageSourcesSectionName, source.Name, "true");
        }

        public bool IsPackageSourceEnabled(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string value = _settingsManager.GetValue(DisabledPackageSourcesSectionName, source.Name);

            // It doesn't matter what value it is.
            // As long as the package source name is persisted in the <disabledPackageSources> section, the source is disabled.
            return String.IsNullOrEmpty(value);
        }

        private class PackageSourceCredential
        {
            public string Username { get; private set; }
            public string Password { get; private set; }
            public bool IsPasswordClearText { get; private set; }

            public PackageSourceCredential(string username, string password, bool isPasswordClearText)
            {
                Username = username;
                Password = password;
                IsPasswordClearText = isPasswordClearText;
            }
        }
    }
}