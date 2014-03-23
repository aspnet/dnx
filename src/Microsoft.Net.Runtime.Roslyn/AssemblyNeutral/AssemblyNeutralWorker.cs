using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.Emit;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class AssemblyNeutralWorker
    {
        private IList<TypeCompilationContext> _typeCompilationContexts = new List<TypeCompilationContext>();
        private readonly IDictionary<string, EmbeddedMetadataReference> _existingReferences;

        public AssemblyNeutralWorker(CSharpCompilation compilation, 
                                     IDictionary<string, EmbeddedMetadataReference> existingReferences)
        {
            OriginalCompilation = compilation;
            _existingReferences = existingReferences;
        }

        public CSharpCompilation OriginalCompilation { get; private set; }

        public CSharpCompilation Compilation { get; private set; }

        public IEnumerable<TypeCompilationContext> TypeCompilations
        {
            get
            {
                return _typeCompilationContexts.Where(t => t.EmitResult != null && t.EmitResult.Success);
            }
        }

        public void FindTypeCompilations(INamespaceOrTypeSymbol symbol)
        {
            if (symbol.IsNamespace)
            {
                foreach (var member in (symbol as INamespaceSymbol).GetMembers())
                {
                    FindTypeCompilations(member);
                }
            }
            else
            {
                var typeSymbol = symbol as ITypeSymbol;
                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    if (attribute.AttributeClass.Name == "AssemblyNeutralAttribute")
                    {
                        _typeCompilationContexts.Add(new TypeCompilationContext(this, typeSymbol));
                    }
                }
            }
        }

        public void OrderTypeCompilations()
        {
            var state = new OrderingState();
            state.Interesting = _typeCompilationContexts.ToDictionary(t => (ISymbol)t.TypeSymbol, t => t);
            state.Order();
            _typeCompilationContexts = state.Ordered;
        }

        public IList<Diagnostic> GenerateTypeCompilations()
        {
            var diagnostics = new List<Diagnostic>();

            foreach (var context in _typeCompilationContexts)
            {
                if (_existingReferences.ContainsKey(context.AssemblyName))
                {
                    continue;
                }

                var result = context.Generate(_existingReferences);

                diagnostics.AddRange(result.Diagnostics);
            }

            return diagnostics;
        }

        public void Generate()
        {
            Compilation = OriginalCompilation;

            var treeRemoveNodes = new Dictionary<SyntaxTree, List<SyntaxNode>>();
            foreach (var neutralTypeContext in _typeCompilationContexts)
            {
                if (neutralTypeContext.EmitResult != null && !neutralTypeContext.EmitResult.Success)
                {
                    // Only skip this reference if there was a failed emit
                    continue;
                }

                MetadataReference neutralReference = neutralTypeContext.Reference;

                EmbeddedMetadataReference assemblyNeutralReference;
                if (_existingReferences.TryGetValue(neutralTypeContext.AssemblyName, out assemblyNeutralReference))
                {
                    neutralReference = assemblyNeutralReference.MetadataReference;
                }

                Compilation = Compilation.AddReferences(neutralReference);

                foreach (var syntaxReference in neutralTypeContext.TypeSymbol.DeclaringSyntaxReferences)
                {
                    var tree = syntaxReference.SyntaxTree;

                    List<SyntaxNode> removeNodes;
                    if (!treeRemoveNodes.TryGetValue(tree, out removeNodes))
                    {
                        removeNodes = new List<SyntaxNode>();
                        treeRemoveNodes.Add(tree, removeNodes);
                    }
                    removeNodes.Add(syntaxReference.GetSyntax());
                }
            }

            foreach (var treeRemoveNode in treeRemoveNodes)
            {
                var tree = treeRemoveNode.Key;
                var removeNodes = treeRemoveNode.Value;

                var root = tree.GetRoot();

                // what it looks like when removed
                var newRoot = root.RemoveNodes(removeNodes, SyntaxRemoveOptions.KeepDirectives);
                var newTree = SyntaxFactory.SyntaxTree(newRoot, tree.FilePath, tree.Options);

                // update compilation with code removed
                Compilation = Compilation.ReplaceSyntaxTree(tree, newTree);
            }
        }

        public class OrderingState
        {
            public OrderingState()
            {
                Ordered = new List<TypeCompilationContext>();
            }

            public Dictionary<ISymbol, TypeCompilationContext> Interesting { get; set; }
            public Stack<OrderingFrame> Stack = new Stack<OrderingFrame>();
            public List<TypeCompilationContext> Ordered { get; set; }

            public void Order()
            {
                var types = Interesting.Values.ToArray();
                foreach (var type in types)
                {
                    Add(type, StrengthKind.None);
                }
            }

            public void Add(TypeCompilationContext type, StrengthKind strength)
            {
                var frame = new OrderingFrame
                {
                    Previous = Stack.FirstOrDefault(),
                    Type = type,
                    Strength = strength
                };

                if (frame.Previous != null &&
                    frame.Previous.Type != null &&
                    frame.Previous.Type != frame.Type)
                {
                    StrengthKind priorStrength;
                    if (!frame.Previous.Type.Requires.TryGetValue(type, out priorStrength) ||
                        priorStrength < strength)
                    {
                        frame.Previous.Type.Requires[type] = strength;
                    }
                }

                if (Ordered.Contains(type) || frame.IsPrevented())
                {
                    return;
                }

                Stack.Push(frame);
                type.SymbolUsage(ShallowUsage, DeepUsage);
                if (!Ordered.Contains(type))
                {
                    Ordered.Add(type);
                }
                Stack.Pop();
            }

            public void ShallowUsage(ISymbol symbol)
            {
                if (symbol == null)
                {
                    return;
                }
                TypeCompilationContext type;
                if (!Interesting.TryGetValue(symbol, out type))
                {
                    return;
                }
                Add(type, StrengthKind.ShallowUsage);
            }

            public void DeepUsage(ISymbol symbol)
            {
                if (symbol == null)
                {
                    return;
                }
                TypeCompilationContext type;
                if (!Interesting.TryGetValue(symbol, out type))
                {
                    return;
                }
                Add(type, StrengthKind.DeepUsage);
            }

            public enum StrengthKind { None, ShallowUsage, DeepUsage };

            public class OrderingFrame
            {
                public OrderingFrame Previous { get; set; }

                public TypeCompilationContext TypeCompilation { get; set; }

                public TypeCompilationContext Type { get; set; }

                public StrengthKind Strength { get; set; }

                public bool IsPrevented()
                {
                    var scanStrength = Strength;
                    for (var scan = Previous; scan != null; scan = scan.Previous)
                    {
                        if (scan.Strength < scanStrength)
                        {
                            return false;
                        }
                        if (scan.Type == Type)
                        {
                            return true;
                        }
                        scanStrength = scan.Strength;
                    }
                    return false;
                }
            }
        }
    }
}
