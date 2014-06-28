// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace NuGet
{
    public class CryptoHashProvider : IHashProvider
    {
        /// <summary>
        /// Server token used to represent that the hash being used is SHA 256
        /// </summary>
        private const string SHA256HashAlgorithm = "SHA256";

        public CryptoHashProvider()
        {
        }

        public byte[] CalculateHash(Stream stream)
        {
            using (var hashAlgorithm = new SHA256CryptoServiceProvider())
            {
                return hashAlgorithm.ComputeHash(stream);
            }
        }

        public byte[] CalculateHash(byte[] data)
        {
            using (var hashAlgorithm = new SHA256CryptoServiceProvider())
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