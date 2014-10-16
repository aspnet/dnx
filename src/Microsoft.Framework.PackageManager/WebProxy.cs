// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if ASPNETCORE50
using System;
using System.Net;

namespace Microsoft.Framework.PackageManager
{
    public class WebProxy : IWebProxy
    {
        private readonly Uri _uri;

        public WebProxy(Uri uri)
        {
            _uri = uri;
        }

        public ICredentials Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            return _uri;
        }

        public bool IsBypassed(Uri host)
        {
            return false;
        }
    }
}
#endif