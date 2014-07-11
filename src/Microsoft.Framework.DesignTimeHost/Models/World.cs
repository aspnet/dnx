// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;

namespace Microsoft.Framework.DesignTimeHost.Models
{
    public class World
    {
        public ProjectInformationMessage Configurations { get; set; }
        public ReferencesMessage References { get; set; }
        public DiagnosticsMessage Diagnostics { get; set; }
        public SourcesMessage Sources { get; set; }
    }
}
