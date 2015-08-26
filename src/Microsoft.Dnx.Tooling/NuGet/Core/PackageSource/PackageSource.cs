// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Microsoft.Framework.Internal;

namespace NuGet
{
    [DataContract]
    public class PackageSource : IEquatable<PackageSource>
    {
        [DataMember]
        public string Name { get; private set; }

        [DataMember]
        public string Source { get; private set; }

        public ISettings Origin { get; private set; }

        /// <summary>
        /// This does not represent just the NuGet Official Feed alone
        /// It may also represent a Default Package Source set by Configuration Defaults
        /// </summary>
        public bool IsOfficial { get; set; }

        public bool IsMachineWide { get; set; }

        public bool IsEnabled { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public bool IsPasswordClearText { get; set; }

        public PackageSource(string source) :
            this(source, source, isEnabled: true)
        {
        }

        public PackageSource(string source, string name) :
            this(source, name, isEnabled: true)
        {
        }

        public PackageSource(string source, string name, bool isEnabled)
            : this(source, name, isEnabled, isOfficial: false)
        {
        }

        public PackageSource(string source, string name, bool isEnabled, bool isOfficial)
            : this(source, name, isEnabled, isOfficial, origin: null)
        {
        }

        public PackageSource(string source, string name, bool isEnabled, bool isOfficial, ISettings origin)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            Source = Environment.ExpandEnvironmentVariables(source);
            IsEnabled = isEnabled;
            IsOfficial = isOfficial;
            Origin = origin;
        }

        public bool Equals(PackageSource other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Source, other.Source, StringComparison.Ordinal) &&
                string.Equals(UserName, other.UserName, StringComparison.Ordinal) &&
                string.Equals(Password, other.Password, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            var source = obj as PackageSource;
            if (source != null)
            {
                return Equals(source);
            }
            return base.Equals(obj);
        }

        public override string ToString()
        {
            return Name + " [" + Source + "]";
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Name, StringComparer.OrdinalIgnoreCase)
                .Add(Source, StringComparer.Ordinal)
                .Add(UserName, StringComparer.Ordinal)
                .Add(Password, StringComparer.Ordinal);
        }

        public PackageSource Clone()
        {
            return new PackageSource(Source, Name, IsEnabled, IsOfficial, Origin) { UserName = UserName, Password = Password, IsPasswordClearText = IsPasswordClearText, IsMachineWide = IsMachineWide };
        }
    }
}