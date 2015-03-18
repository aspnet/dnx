// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class PluginResponseMessage
    {
        public string MessageName { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}