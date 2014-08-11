// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime.Roslyn;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class FrameworkData
    {
        public string FrameworkName { get; set; }
        public string LongFrameworkName { get; set; }
        public string FriendlyFrameworkName { get; set; }
        public CompilationSettings CompilationSettings { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as FrameworkData;

            return other != null && 
                   string.Equals(FrameworkName, other.FrameworkName) &&
                   string.Equals(LongFrameworkName, other.LongFrameworkName) &&
                   string.Equals(FriendlyFrameworkName, other.FriendlyFrameworkName) &&
                   object.Equals(CompilationSettings, other.CompilationSettings);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}