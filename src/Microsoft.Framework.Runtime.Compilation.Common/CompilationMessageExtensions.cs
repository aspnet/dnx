// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.Runtime
{
    internal static class CompilationMessageExtensions
    {
        /// <summary>
        /// Returns true if <paramref name="messages"/> has at least one message with <see cref="CompilationMessageSeverity.Error"/>.
        /// </summary>
        /// <param name="messages">Sequence of <see cref="ICompilationMessage"/>.</param>
        /// <returns><c>true</c> if any messages is an error message, <c>false</c> otherwise.</returns>
        public static bool HasErrors(this IEnumerable<ICompilationMessage> messages)
        {
            return messages.Any(m => m.Severity == CompilationMessageSeverity.Error);
        }
    }
}