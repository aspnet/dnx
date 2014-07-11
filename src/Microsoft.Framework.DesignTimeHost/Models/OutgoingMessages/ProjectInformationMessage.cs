// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ProjectInformationMessage
    {
        public string ProjectName { get; set; }

        // TODO: This is for legacy scenarios. Remove when
        // this is fully implemented
        public IList<FrameworkData> Configurations { get; set; }

        public IList<FrameworkData> Frameworks { get; set; }

        // We'll eventually move this to the configurations property
        public IList<ConfigurationData> ProjectConfigurations { get; set; }

        public IDictionary<string, string> Commands { get; set; }
    }
}