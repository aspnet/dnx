// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.PackageManager
{
    public class NullReport : IReport
    {
        public void WriteLine(string message)
        {
            // Consume the write operation and do nothing
            // Used when verbose option is not specified
        }
    }
}