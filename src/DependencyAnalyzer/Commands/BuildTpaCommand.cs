// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DependencyAnalyzer.Util;
using Microsoft.Framework.Runtime;

namespace DependencyAnalyzer.Commands
{
    /// <summary>
    /// Command to build the minimal TPA list
    /// </summary>
    public class BuildTpaCommand
    {
        private readonly IApplicationEnvironment _environment;
        private readonly string _assemblyFolder;
        private readonly string _sourceFile;

        public BuildTpaCommand(IApplicationEnvironment environment, string assemblyFolder, string sourceFile)
        {
            _environment = environment;
            _assemblyFolder = assemblyFolder;
            _sourceFile = sourceFile;
        }

        /// <summary>
        /// Execute the command 
        /// </summary>
        /// <returns>Returns 0 for success, otherwise 1.</returns>
        public int Execute()
        {
            var accessor = new CacheContextAccessor();
            var cache = new Cache(accessor);

            var finder = new DependencyFinder(accessor, cache, _environment, _assemblyFolder);

            ICollection<string> tpa = finder.GetDependencies("dotnet.core45.managed");

            // ordering the tpa list make it easier to compare the difference
            UpdateSourceFile(tpa.OrderBy(one => one).ToArray());

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

            File.WriteAllLines(_sourceFile, content.ToArray());

            return true;
        }
    }
}