// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.DependencyAnalyzer
{
    /// <summary>
    /// Command to build the minimal TPA list
    /// </summary>
    public class BuildTpaCommand
    {
        public string KreRoot { get; internal set; }
        public string Output { get; internal set; }
        public string CoreClrRoot { get; internal set; }
        public Reports Reports { get; internal set; }

        /// <summary>
        /// Execute the command 
        /// </summary>
        /// <returns>Returns 0 for success, otherwise 1.</returns>
        public int Execute()
        {
            if (string.IsNullOrEmpty(KreRoot) || !Directory.Exists(KreRoot))
            {
                Reports.Error.WriteLine("A valid path to the KRE folder is required");
                return 1;
            }

            if (string.IsNullOrEmpty(CoreClrRoot) || !Directory.Exists(CoreClrRoot))
            {
                Reports.Error.WriteLine("A valid path to the CoreCLR folder is required.");
                return 1;
            }

            var finder = new DependencyFinder(
                KreRoot,
                VersionUtility.ParseFrameworkName("aspnetcore50"),
                hostContext => new DependencyResolverForCoreCLR(hostContext, CoreClrRoot));

            ICollection<string> tpa = finder.GetDependencies("klr.core45.managed");

            // ordering the tpa list make it easier to compare the difference
            UpdateSourceFile(tpa.Select(one => Path.GetFileNameWithoutExtension(one))
                                .OrderBy(one => one)
                                .ToArray());

            return 0;
        }

        private bool UpdateSourceFile(string[] tpa)
        {
            var content = new List<string>();

            content.Add(@"// This file will be dynamically updated during build to generate a ");
            content.Add(@"// minimal trusted platform assemblies list");
            content.Add(string.Empty);
            content.Add("#include \"stdafx.h\"");
            content.Add("#include \"tpa.h\"");
            content.Add(string.Empty);
            content.Add("BOOL CreateTpaBase(LPWSTR** ppNames, size_t* pcNames, bool bNative)");
            content.Add("{");
            content.Add("    const size_t count = " + tpa.Length + ";");
            content.Add("    LPWSTR* pArray = new LPWSTR[count];");
            content.Add(string.Empty);
            content.Add("    if (bNative)");
            content.Add("    {");

            for (int i = 0; i < tpa.Length; ++i)
            {
                content.Add(string.Format("        pArray[{0}] = _wcsdup(L\"{1}{2}\");", i, tpa[i], ".ni.dll"));
            }

            content.Add("    }");
            content.Add("    else");
            content.Add("    {");

            for (int i = 0; i < tpa.Length; ++i)
            {
                content.Add(string.Format("        pArray[{0}] = _wcsdup(L\"{1}{2}\");", i, tpa[i], ".dll"));
            }

            content.Add("    }");
            content.Add(string.Empty);
            content.Add("    *ppNames = pArray;");
            content.Add("    *pcNames = count;");
            content.Add(string.Empty);
            content.Add("    return true;");
            content.Add("}");
            content.Add(string.Empty);
            content.Add("BOOL FreeTpaBase(const LPWSTR* values, const size_t count)");
            content.Add("{");
            content.Add("    for (size_t idx = 0; idx < count; ++idx)");
            content.Add("    {");
            content.Add("        delete[] values[idx];");
            content.Add("    }");
            content.Add(string.Empty);
            content.Add("    delete[] values;");
            content.Add(string.Empty);
            content.Add("    return true;");
            content.Add("}");

            if (string.IsNullOrEmpty(Output))
            {
                Reports.Information.WriteLine("Write TPA to console");

                for (int i = 0; i < content.Count; ++i)
                {
                    Reports.Information.WriteLine("{0,-4}{1}", i + 1, content[i]);
                }
            }
            else
            {
                Reports.Information.WriteLine("Write TPA to " + Output);
                File.WriteAllLines(Output, content.ToArray());
            }

            return true;
        }
    }
}