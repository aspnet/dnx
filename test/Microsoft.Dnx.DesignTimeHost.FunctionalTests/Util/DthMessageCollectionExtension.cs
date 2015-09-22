// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Util
{
    public static class DthMessageCollectionExtension
    {
        public static DthMessage RetrieveSingle(this IEnumerable<DthMessage> messages,
                                                string typename)
        {
            var result = messages.SingleOrDefault(msg => string.Equals(msg.MessageType, typename, StringComparison.Ordinal));

            if (result == null)
            {
                if (ContainsMessage(messages, typename))
                {
                    Assert.False(true, $"More than one {typename} messages exist.");
                }
                else
                {
                    Assert.False(true, $"{typename} message doesn't exists.");
                }
            }

            return result;
        }

        public static bool ContainsMessage(this IEnumerable<DthMessage> messages,
                                           string typename)
        {
            return messages.FirstOrDefault(msg => string.Equals(msg.MessageType, typename, StringComparison.Ordinal)) != null;
        }
    }
}
