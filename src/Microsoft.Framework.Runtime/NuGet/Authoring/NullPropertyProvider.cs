// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet
{
    public class NullPropertyProvider : IPropertyProvider
    {
        private static readonly NullPropertyProvider _instance = new NullPropertyProvider();
        private NullPropertyProvider()
        {
        }

        public static NullPropertyProvider Instance
        {
            get
            {
                return _instance;
            }
        }

        public string GetPropertyValue(string propertyName)
        {
            return null;
        }
    }
}
