// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace NuGet
{
    public class CryptoHashProvider : IHashProvider
    {
        public CryptoHashProvider()
        {
        }

        public byte[] CalculateHash(Stream stream)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                return hashAlgorithm.ComputeHash(stream);
            }
        }

        public byte[] CalculateHash(byte[] data)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                return hashAlgorithm.ComputeHash(data);
            }
        }

        public bool VerifyHash(byte[] data, byte[] hash)
        {
            byte[] dataHash = CalculateHash(data);
            return Enumerable.SequenceEqual(dataHash, hash);
        }
    }
}