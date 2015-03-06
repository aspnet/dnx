// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.Framework.Runtime.Roslyn.Common
{
    public static class CompilationDefaults
    {
        public static readonly ImmutableArray<byte> PublicKey = GetPublicKey();
        public static readonly string PublicKeyHex = @"0024000004800000940000000602000000240000525341310004000001000100bbe2884a1ce0432e3d6fb9225fd417a67a116c714bfec6a27ef678a1906374ddd6bc9c34c7c42c241b46005668b4aabbd1a5c80087895fc8329b2c14ee41b07e86275e03de27951630b3223f602f053d1d7d8fbc7e86e2f549f9b47bd0e4923918c537d86a9d3c4fa5ae5cf9f3ad15fd475aeba2dd3ad77fb56c9ad23939c9a2";

        private static ImmutableArray<byte> GetPublicKey()
        {
            var publicKey = new byte[] { 0, 36, 0, 0, 4, 128, 0, 0, 148, 0, 0, 0, 6, 2, 0, 0, 0, 36, 0, 0, 82, 83, 65, 49, 0, 4, 0, 0, 1, 0, 1, 0, 187, 226, 136, 74, 28, 224, 67, 46, 61, 111, 185, 34, 95, 212, 23, 166, 122, 17, 108, 113, 75, 254, 198, 162, 126, 246, 120, 161, 144, 99, 116, 221, 214, 188, 156, 52, 199, 196, 44, 36, 27, 70, 0, 86, 104, 180, 170, 187, 209, 165, 200, 0, 135, 137, 95, 200, 50, 155, 44, 20, 238, 65, 176, 126, 134, 39, 94, 3, 222, 39, 149, 22, 48, 179, 34, 63, 96, 47, 5, 61, 29, 125, 143, 188, 126, 134, 226, 245, 73, 249, 180, 123, 208, 228, 146, 57, 24, 197, 55, 216, 106, 157, 60, 79, 165, 174, 92, 249, 243, 173, 21, 253, 71, 90, 235, 162, 221, 58, 215, 127, 181, 108, 154, 210, 57, 57, 201, 162 };

            return ImmutableArray.Create(publicKey);
        }
    }
}