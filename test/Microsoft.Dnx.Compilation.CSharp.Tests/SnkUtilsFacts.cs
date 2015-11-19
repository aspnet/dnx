// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class SnkUtilsFacts
    {
        [Fact]
        public void ExtractPublicKeyReturnsPublicKeyFromPrivatePublicKeyPair()
        {
            var keyPair =
                "07020000002400005253413200040000010001004B8DA89A5A03625E7BA3C17639EA8EAC91A07CC2" +
                "BB2F36857FCA73B5CB52CD781A6EB1E14198AD82F9F713F548385DF70C18EC12BC02181AF5EAACC9" +
                "390F790ED6485CD4CAE3684BDDAB6896DF8835E2F19EFE7FF416E8F7AF3A7605605BE48947850383" +
                "418B9DC10BD00DECCC45C2F2AB68B83D00F118E4519BB828AE3078C74F3FD9D6B638EC3F6552BDCF" +
                "9ACA84B5E65A0EC60857D366D5E3369994F95CB68100695681A541A30D811BDD00B0481B17F8D621" +
                "E77D3E9C917DD98F8AE380F64573364098F1426E901E294D482F59F4C79FAF45D4EEC4D2A93DA3D3" +
                "36B6FF0553B89602F8FE747367A7207C41021F4433A84926A356CE9420ED30492E6F27CF279D56B2" +
                "872EA654092329367D58DB5C98DC4405DBFA7921A68F24D0B05B60FE73317561AFD626452715660A" +
                "ECB0F0A81D8EB381A61B137A2B0188B00A82F853B9A01F917D35A0E5476622F736F9B674B9D87E8D" +
                "FB2170C803713F6113F21ABF837E2B3D9D2F0C9051A36D8D73F58E64A0FC7C59BED7E79E1077755D" +
                "18CCC061AEBAE8B805E30A0DD0D78D6EBBE71ADC1B973D9AAB0E0BB52E784942712A9842E5624350" +
                "B526F6CF37F8166373A7F76438D5F8F238889C438465789E946030AEB9160FB83F45E5AA3F974F83" +
                "D6BC1E80A603BFE12D494806802DB203A82906A29D846C5AD9B0DF7009C0E4205A5637C335F328DA" +
                "726DAE93CE761DA5B279FE9137FB86018B4D5872C546243CAD2D8C5626C58188B26A7B240D2DC733" +
                "60588B40E5F79E8BD0D627E1FE9B38483CB579F04EFC550C776DFE0459C384B7F74D711C";

            var extractedPublicKey = SnkUtils.ExtractPublicKey(HexToBin(keyPair));

            var b = new byte[extractedPublicKey.Length];
            extractedPublicKey.CopyTo(b);

            var s = BitConverter.ToString(b).Replace("-", "");

            var expectedKey = HexToBin(
                "00240000048000009400000006020000002400005253413100040000010001004B8DA89A5A03625E" +
                "7BA3C17639EA8EAC91A07CC2BB2F36857FCA73B5CB52CD781A6EB1E14198AD82F9F713F548385DF7" +
                "0C18EC12BC02181AF5EAACC9390F790ED6485CD4CAE3684BDDAB6896DF8835E2F19EFE7FF416E8F7" +
                "AF3A7605605BE48947850383418B9DC10BD00DECCC45C2F2AB68B83D00F118E4519BB828AE3078C7");

            Assert.Equal(expectedKey, extractedPublicKey);
        }

        [Fact]
        public void ExtractPublicKeyReturnsTheSameKeyIfOnlyPublicKeyProvided()
        {
            var sourceKey = HexToBin(
                "002400000D8000009400000006020000002400005253413100040000010001004B8DA89A5A03625E" +
                "7BA3C17639EA8EAC91A07CC2BB2F36857FCA73B5CB52CD781A6EB1E14198AD82F9F713F548385DF7" +
                "0C18EC12BC02181AF5EAACC9390F790ED6485CD4CAE3684BDDAB6896DF8835E2F19EFE7FF416E8F7" +
                "AF3A7605605BE48947850383418B9DC10BD00DECCC45C2F2AB68B83D00F118E4519BB828AE3078C7");

            var extractedPublicKey = SnkUtils.ExtractPublicKey(sourceKey);

            Assert.Equal(sourceKey, extractedPublicKey);
        }

        [Fact]
        public void ExtractPublicKeyThrowsForInvalidKeyBlobs()
        {
            var invalidKeyBlobs = new[]
            {
                string.Empty,
                new string('0', 160 * 2), // 160 * 2 - the length of a public key, 2 - 2 chars per byte
                new string('0', 596 * 2), // 596 * 2 - the length of a key pair, 2 - 2 chars per byte
                "0702000000240000DEADBEEF" + new string('0', 584 * 2), // private key blob without magic private key
                "002400000D800000940000000602000000240000DEADBEEF" + new string('0', 136 * 2), // public key blob without magic public key
            };

            foreach (var key in invalidKeyBlobs)
            {
                Assert.Equal("Invalid key file.",
                    Assert.Throws<InvalidOperationException>(() => SnkUtils.ExtractPublicKey(HexToBin(key))).Message);
            }
        }

        private static byte[] HexToBin(string input)
        {
            Debug.Assert(input != null && (input.Length & 1) == 0, "invalid input string.");

            var result = new byte[input.Length >> 1];

            for (var i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(input.Substring(i << 1, 2), NumberStyles.HexNumber);
            }

            return result;
        }
    }
}
