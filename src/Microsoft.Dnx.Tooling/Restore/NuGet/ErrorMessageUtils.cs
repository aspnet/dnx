// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Dnx.Tooling.Restore.NuGet
{
    internal static class ErrorMessageUtils
    {
        public static string GetFriendlyTimeoutErrorMessage(TaskCanceledException ex, bool isFinalAttempt, bool ignoreFailure)
        {
            // "A task was canceled" doesn't make sense to users, use a better error message.
            var reaction = "Retrying";
            if (isFinalAttempt && ignoreFailure)
            {
                reaction = "Ignoring the remote source";
            }
            else if (isFinalAttempt)
            {
                reaction = "Exiting";
            }

            return $"HTTP request timed out. {reaction}.";
        }
    }
}
