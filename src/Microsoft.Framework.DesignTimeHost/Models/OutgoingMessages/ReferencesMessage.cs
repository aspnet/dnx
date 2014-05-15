// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ReferencesMessage
    {
        public string RootDependency { get; set; }
        public string LongFrameworkName { get; set; }
        public string FriendlyFrameworkName { get; set; }
        public IList<string> ProjectReferences { get; set; }
        public IList<string> FileReferences { get; set; }
        public IDictionary<string, byte[]> RawReferences { get; set; }
        public IDictionary<string, ReferenceDescription> Dependencies { get; set; }
    }
}