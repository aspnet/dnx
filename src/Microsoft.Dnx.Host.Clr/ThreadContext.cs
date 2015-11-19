// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Threading;

namespace Microsoft.Dnx.Host.Clr
{
    internal class ThreadContext
    {
        private readonly CultureInfo _culture;
        private readonly CultureInfo _uiCulture;

        public ThreadContext(Thread currentThread)
        {
            _culture = currentThread.CurrentCulture;
            _uiCulture = currentThread.CurrentUICulture;
        }

        public ThreadContext CaptureAndReplace(Thread currentThread)
        {
            var currentContext = new ThreadContext(currentThread);

            currentThread.CurrentCulture = _culture;
            currentThread.CurrentUICulture = _uiCulture;

            return currentContext;
        }
    }
}