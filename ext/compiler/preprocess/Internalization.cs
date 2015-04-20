// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Framework.Runtime.Roslyn;

namespace Runtime.Ext.Compiler.Preprocess
{
    public class Internalization : ICompileModule
    {
        public Internalization(IServiceProvider services)
        {
        }

        public void BeforeCompile(BeforeCompileContext context)
        {
            var candidates = new List<string>();

            candidates.Add(Path.Combine(context.ProjectContext.ProjectDirectory, "..", "..", "submodules"));

            if (context.ProjectContext.Name != "Microsoft.Framework.Runtime")
            {
                candidates.Add(Path.Combine(context.ProjectContext.ProjectDirectory, "..", "Microsoft.Framework.Runtime.Hosting"));
            }

            var submodulesDir = Path.Combine(context.ProjectContext.ProjectDirectory, "..", "..", "submodules");
            var replacementDict = new Dictionary<SyntaxTree, SyntaxTree>();
            var removeList = new List<SyntaxTree>();

            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                if (string.IsNullOrEmpty(tree.FilePath) ||
                    !candidates.Any(c => IsChildOfDirectory(dir: c, candidate: tree.FilePath)))
                {
                    continue;
                }
                
                if (string.Equals("AssemblyInfo.cs", Path.GetFileName(tree.FilePath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    removeList.Add(tree);
                    continue;
                }

                var root = tree.GetRoot();

                var targetSyntaxKinds = new[] {
                    SyntaxKind.ClassDeclaration,
                    SyntaxKind.InterfaceDeclaration,
                    SyntaxKind.StructDeclaration,
                    SyntaxKind.EnumDeclaration
                };

                var typeDeclarations = root.DescendantNodes()
                    .Where(x => targetSyntaxKinds.Contains(x.Kind()))
                    .OfType<BaseTypeDeclarationSyntax>();
                var publicKeywordTokens = new List<SyntaxToken>();

                foreach (var declaration in typeDeclarations)
                {
                    var publicKeywordToken = declaration.Modifiers
                        .SingleOrDefault(x => x.Kind() == SyntaxKind.PublicKeyword);
                    if (publicKeywordToken != default(SyntaxToken))
                    {
                        publicKeywordTokens.Add(publicKeywordToken);
                    }
                }

                if (publicKeywordTokens.Any())
                {
                    root = root.ReplaceTokens(publicKeywordTokens,
                        (_, oldToken) => SyntaxFactory.ParseToken("internal").WithTriviaFrom(oldToken));
                }

                replacementDict.Add(tree,
                    SyntaxFactory.SyntaxTree(root, tree.Options, tree.FilePath, tree.GetText().Encoding));
            }

            context.Compilation = context.Compilation.RemoveSyntaxTrees(removeList);
            foreach (var pair in replacementDict)
            {
                context.Compilation = context.Compilation.ReplaceSyntaxTree(pair.Key, pair.Value);
            }
        }

        public void AfterCompile(AfterCompileContext context)
        {

        }

        private static bool IsChildOfDirectory(string dir, string candidate)
        {
            dir = Path.GetFullPath(dir);
            dir = dir.EndsWith(Path.DirectorySeparatorChar.ToString()) ? dir : dir + Path.DirectorySeparatorChar;
            candidate = Path.GetFullPath(candidate);
            return candidate.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }
    }
}
