// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ConfigurationsMessage
    {
        public string ProjectName { get; set; }
        public IList<ConfigurationData> Configurations { get; set; }
        public IDictionary<string, string> Commands { get; set; }
    }
}