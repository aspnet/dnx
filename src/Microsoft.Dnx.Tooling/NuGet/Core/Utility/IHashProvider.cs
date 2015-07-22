// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet
{
    public interface IHashProvider
    {
        byte[] CalculateHash(Stream stream);

        byte[] CalculateHash(byte[] data);

        bool VerifyHash(byte[] data, byte[] hash);
    }
}
