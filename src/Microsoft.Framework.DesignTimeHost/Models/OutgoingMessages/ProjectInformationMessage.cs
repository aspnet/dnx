// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ProjectInformationMessage
    {
        public string ProjectName { get; set; }

        public IList<FrameworkData> Frameworks { get; set; }

        // We'll eventually move this to the configurations property
        public IList<ConfigurationData> ProjectConfigurations { get; set; }

        public IDictionary<string, string> Commands { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ProjectInformationMessage;

            return other != null &&
                   string.Equals(ProjectName, other.ProjectName) &&
                   Enumerable.SequenceEqual(Frameworks, other.Frameworks) &&
                   Enumerable.SequenceEqual(ProjectConfigurations, other.ProjectConfigurations) &&
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