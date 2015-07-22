// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling
{
    public enum DnuFolderPath
    {
        MachineWideSettingsBaseDirectory,
        MachineWideConfigDirectory,
        UserSettingsDirectory,
        HttpCacheDirectory,
        DnxHome,
        DefaultDnxHome,
        DnxGlobalPath,
        DefaultMsBuildPath
    }

    public static class DnuEnvironment
    {
        public static string GetFolderPath(DnuFolderPath folder)
        {
            switch (folder)
            {
                case DnuFolderPath.MachineWideSettingsBaseDirectory:
                    var appData = string.Empty;
                    if (RuntimeEnvironmentHelper.IsWindows)
                    {
                        appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    }
                    else
                    {
                        // Only super users have write access to common app data folder on *nix,
                        // so we use roaming local app data folder instead
                        appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    }
                    return Path.Combine(appData, "NuGet");
                case DnuFolderPath.MachineWideConfigDirectory:
                    return Path.Combine(GetFolderPath(DnuFolderPath.MachineWideSettingsBaseDirectory),
                        "Config");
                case DnuFolderPath.UserSettingsDirectory:
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "NuGet");
                case DnuFolderPath.DefaultDnxHome:
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    return Path.Combine(userProfile, Constants.DefaultLocalRuntimeHomeDir);
                case DnuFolderPath.DnxHome:
                    var runtimeHome = Environment.GetEnvironmentVariable(EnvironmentNames.Home);
                    if (string.IsNullOrEmpty(runtimeHome))
                    {
                        runtimeHome = GetFolderPath(DnuFolderPath.DefaultDnxHome);
                    }
                    return runtimeHome;
                case DnuFolderPath.HttpCacheDirectory:
                    var localAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    return Path.Combine(localAppDataFolder, "dnu", "cache");
                case DnuFolderPath.DnxGlobalPath:
                    return Environment.GetEnvironmentVariable(EnvironmentNames.GlobalPath);
                case DnuFolderPath.DefaultMsBuildPath:
                    var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    if (string.IsNullOrEmpty(programFilesPath))
                    {
                        // On 32-bit Windows
                        programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    }
                    return Path.Combine(programFilesPath, "MSBuild", "14.0", "Bin", "MSBuild.exe");
                default:
                    return null;
            }
        }

#if DNXCORE50
        private static class Environment
        {
            public static string NewLine { get; } = System.Environment.NewLine;

            public static string GetEnvironmentVariable(string variable)
            {
                return System.Environment.GetEnvironmentVariable(variable);
            }

            public static string ExpandEnvironmentVariables(string name)
            {
                return System.Environment.ExpandEnvironmentVariables(name);
            }

            public static string GetFolderPath(SpecialFolder folder)
            {
                switch (folder)
                {
                    case SpecialFolder.ProgramFilesX86:
                        return GetEnvironmentVariable("PROGRAMFILES(X86)");
                    case SpecialFolder.ProgramFiles:
                        return GetEnvironmentVariable("PROGRAMFILES");
                    case SpecialFolder.UserProfile:
                        return GetHome();
                    case SpecialFolder.CommonApplicationData:
                        if (RuntimeEnvironmentHelper.IsWindows)
                        {
                            return FirstNonEmpty(
                                () => GetEnvironmentVariable("PROGRAMDATA"),
                                () => GetEnvironmentVariable("ALLUSERSPROFILE"));
                        }
                        else
                        {
                            return "/usr/share";
                        }
                    case SpecialFolder.ApplicationData:
                        if (RuntimeEnvironmentHelper.IsWindows)
                        {
                            return GetEnvironmentVariable("APPDATA");
                        }
                        else
                        {
                            return FirstNonEmpty(
                                () => GetEnvironmentVariable("XDG_CONFIG_HOME"),
                                () => Path.Combine(GetHome(), ".config"));
                        }
                    case SpecialFolder.LocalApplicationData:
                        if (RuntimeEnvironmentHelper.IsWindows)
                        {
                            return GetEnvironmentVariable("LOCALAPPDATA");
                        }
                        else
                        {
                            return FirstNonEmpty(
                                () => GetEnvironmentVariable("XDG_DATA_HOME"),
                                () => Path.Combine(GetHome(), ".local", "share"));
                        }
                    default:
                        return null;
                }
            }

            private static string GetHome()
            {
                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    return FirstNonEmpty(
                        () => GetEnvironmentVariable("USERPROFILE"),
                        () => GetEnvironmentVariable("HOMEDRIVE") + GetEnvironmentVariable("HOMEPATH"));
                }
                else
                {
                    return GetEnvironmentVariable("HOME");
                }
            }

            private static string FirstNonEmpty(params Func<string>[] providers)
            {
                foreach (var p in providers)
                {
                    var value = p();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
                return null;
            }

            public enum SpecialFolder
            {
                ProgramFilesX86,
                ProgramFiles,
                UserProfile,
                CommonApplicationData,
                ApplicationData,
                LocalApplicationData
            }
        }
#endif
    }
}