// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ProjectMessage
    {
        public string ProjectName { get; set; }

        public IList<FrameworkData> Frameworks { get; set; }

        public IList<ConfigurationData> Configurations { get; set; }

        public IDictionary<string, string> Commands { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ProjectMessage;

            return other != null &&
                   string.Equals(ProjectName, other.ProjectName) &&
                   Enumerable.SequenceEqual(Frameworks, other.Frameworks) &&
                   Enumerable.SequenceEqual(Configurations, other.Configurations) &&
                   Enumerable.SequenceEqual(Commands, other.Commands);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}