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

#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
using EmitResult = Microsoft.CodeAnalysis.Emit.CommonEmitResult;
using AttributeData = Microsoft.CodeAnalysis.CommonAttributeData;
#endif

namespace Microsoft.Net.Runtime.Roslyn.AssemblyNeutral
{
    public class CompilationContext
    {
        private readonly Dictionary<string, EmitResult> _failedTypeCompilations = new Dictionary<string, EmitResult>();
        private readonly List<EmitResult> _failedCompilations = new List<EmitResult>();

        public Project Project { get; set; }
        public CSharpCompilation OriginalCompilation { get; set; }
        public CSharpCompilation Compilation { get; set; }
        public IList<EmitResult> FailedCompilations { get { return _failedCompilations; } }

        public IEnumerable<TypeCompilationContext> SuccessfulTypeCompilationContexts
        {
            get
            {
                return TypeCompilationContexts.Where(t => !_failedTypeCompilations.ContainsKey(t.AssemblyName));
            }
        }

        public IList<TypeCompilationContext> TypeCompilationContexts = new List<TypeCompilationContext>();

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
                        TypeCompilationContexts.Add(new TypeCompilationContext
                        {
                            Context = this,
                            TypeSymbol = typeSymbol
                        });
                    }
                }
            }
        }

        public void OrderTypeCompilations()
        {
            var state = new OrderingState();
            state.Interesting = TypeCompilationContexts.ToDictionary(t => (ISymbol)t.TypeSymbol, t => t);
            state.Order();
            TypeCompilationContexts = state.Ordered;
        }

        public void GenerateTypeCompilations()
        {
            foreach (var t in TypeCompilationContexts)
            {
                var result = t.Generate();

                if (result == null || result.Success)
                {
                    continue;
                }

                _failedTypeCompilations[t.AssemblyName] = result;

                FailedCompilations.Add(result);
            }
        }

        public void Generate(IDictionary<string, MetadataReference> references)
        {
            Compilation = OriginalCompilation;

            Dictionary<SyntaxTree, List<SyntaxNode>> treeRemoveNodes = new Dictionary<SyntaxTree, List<SyntaxNode>>();
            foreach (var neutralTypeContext in TypeCompilationContexts)
            {
                MetadataReference neutralReference;
                if (!references.TryGetValue(neutralTypeContext.AssemblyName, out neutralReference))
                {
                    neutralReference = neutralTypeContext.Reference;
                }

                if (!_failedTypeCompilations.ContainsKey(neutralTypeContext.AssemblyName))
                {
                    Compilation = Compilation.AddReferences(neutralReference);
                }

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

    public class TypeCompilationContext
    {
        public TypeCompilationContext()
        {
            Requires = new Dictionary<TypeCompilationContext, CompilationContext.OrderingState.StrengthKind>();
        }

        public CompilationContext Context { get; set; }
        public ITypeSymbol TypeSymbol { get; set; }

        public IDictionary<TypeCompilationContext, CompilationContext.OrderingState.StrengthKind> Requires { get; set; }

        public string AssemblyName
        {
            get
            {
                return TypeSymbol.ContainingNamespace + "." + TypeSymbol.Name;
            }
        }

        public CSharpCompilation Compilation { get; set; }

        public Stream OutputStream { get; set; }
        public MetadataReference Reference { get; set; }
        public EmitResult EmitResult { get; set; }

        public CSharpCompilation ShallowCompilation { get; set; }
        public Stream ShallowOutputStream { get; set; }
        public MetadataReference ShallowReference { get; set; }
        public EmitResult ShallowEmitResult { get; set; }


        public void SymbolUsage(Action<ISymbol> shallowUsage, Action<ISymbol> deepUsage)
        {
            deepUsage(TypeSymbol.BaseType);

            foreach (var symbol in TypeSymbol.AllInterfaces)
            {
                deepUsage(symbol);
            }

            AttributeDataSymbolUsage(TypeSymbol.GetAttributes(), deepUsage);

            foreach (var member in TypeSymbol.GetMembers())
            {
                AttributeDataSymbolUsage(member.GetAttributes(), deepUsage);

                var propertyMember = member as IPropertySymbol;
                var fieldMember = member as IFieldSymbol;
                var methodMember = member as IMethodSymbol;

                if (propertyMember != null)
                {
                    shallowUsage(propertyMember.Type);
                }
                if (fieldMember != null)
                {
                    shallowUsage(fieldMember.Type);
                }
                if (methodMember != null)
                {
                    shallowUsage(methodMember.ReturnType);
                    AttributeDataSymbolUsage(methodMember.GetReturnTypeAttributes(), deepUsage);
                    foreach (var parameter in methodMember.Parameters)
                    {
                        shallowUsage(parameter.Type);
                        AttributeDataSymbolUsage(parameter.GetAttributes(), deepUsage);
                    }
                }
            }
        }

        private void AttributeDataSymbolUsage(IEnumerable<AttributeData> attributeDatas, Action<ISymbol> deepUsage)
        {
            foreach (var attributeData in attributeDatas)
            {
                AttributeDataSymbolUsage(attributeData, deepUsage);
            }
        }

        private void AttributeDataSymbolUsage(AttributeData attributeData, Action<ISymbol> deepUsage)
        {
            deepUsage(attributeData.AttributeClass);
            foreach (var argument in attributeData.ConstructorArguments)
            {
                deepUsage(argument.Type);
            }
            foreach (var argument in attributeData.NamedArguments)
            {
                deepUsage(argument.Value.Type);
            }
        }

        public EmitResult Generate()
        {
            Compilation = CSharpCompilation.Create(
                assemblyName: AssemblyName,
                options: Context.OriginalCompilation.Options,
                references: Context.OriginalCompilation.References);

            foreach (var other in Requires.Keys)
            {
                if (other.EmitResult != null && !other.EmitResult.Success)
                {
                    return null;
                }

                Compilation = Compilation.AddReferences(other.RealOrShallowReference());
            }

            foreach (var syntaxReference in TypeSymbol.DeclaringSyntaxReferences)
            {
                var node = syntaxReference.GetSyntax();
                var tree = syntaxReference.SyntaxTree;
                var root = tree.GetRoot();

                var nodesToRemove = GetNodesToRemove(root, node).ToArray();

                // what it looks like when removed
                var newRoot = root.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepDirectives);
                var newTree = SyntaxFactory.SyntaxTree(newRoot, tree.FilePath, tree.Options);

                // update compilation with code removed
                Compilation = Compilation.AddSyntaxTrees(newTree);
            }

            OutputStream = new MemoryStream();
            EmitResult = Compilation.Emit(OutputStream);
            if (!EmitResult.Success)
            {
                return EmitResult;
            }

            OutputStream.Position = 0;
            Reference = new MetadataImageReference(OutputStream);
            OutputStream.Position = 0;

            return EmitResult;
        }

        private MetadataReference GenerateShallowReference()
        {
            ShallowCompilation = CSharpCompilation.Create(
                assemblyName: AssemblyName,
                options: Context.OriginalCompilation.Options,
                references: Context.OriginalCompilation.References);

            foreach (var other in Requires)
            {
                if (other.Value == CompilationContext.OrderingState.StrengthKind.DeepUsage)
                {
                    ShallowCompilation = ShallowCompilation.AddReferences(other.Key.RealOrShallowReference());
                }
            }

            foreach (var syntaxReference in TypeSymbol.DeclaringSyntaxReferences)
            {
                var node = syntaxReference.GetSyntax();
                var tree = syntaxReference.SyntaxTree;
                var root = tree.GetRoot();

                var nodesToRemove = GetNodesToRemove(root, node);

                foreach (var member in TypeSymbol.GetMembers())
                {
                    foreach (var memberSyntaxReference in member.DeclaringSyntaxReferences)
                    {
                        if (memberSyntaxReference.SyntaxTree == tree)
                        {
                            nodesToRemove = nodesToRemove.Concat(new[] { memberSyntaxReference.GetSyntax() });
                        }
                    }
                }

                var newRoot = root.RemoveNodes(nodesToRemove.ToArray(), SyntaxRemoveOptions.KeepDirectives);
                var newTree = SyntaxFactory.SyntaxTree(newRoot, tree.FilePath, tree.Options);

                ShallowCompilation = ShallowCompilation.AddSyntaxTrees(newTree);
            }
            ShallowOutputStream = new MemoryStream();
            ShallowEmitResult = ShallowCompilation.Emit(ShallowOutputStream);
            ShallowOutputStream.Position = 0;
            ShallowReference = new MetadataImageReference(ShallowOutputStream);
            ShallowOutputStream.Position = 0;
            return ShallowReference;
        }

        public MetadataReference RealOrShallowReference()
        {
            return Reference ?? ShallowReference ?? GenerateShallowReference();
        }

        private IEnumerable<SyntaxNode> GetNodesToRemove(SyntaxNode root, SyntaxNode target)
        {
            for (var scan = target; scan != root; scan = scan.Parent)
            {
                var child = scan;
                var parent = child.Parent;
                foreach (var remove in parent.ChildNodes())
                {
                    if (remove == child)
                    {
                        continue;
                    }

                    if (remove.CSharpKind() == SyntaxKind.UsingDirective)
                    {
                        continue;
                    }

                    if (parent.CSharpKind() == SyntaxKind.NamespaceDeclaration && (
                        (parent as NamespaceDeclarationSyntax).Name == remove))
                    {
                        continue;
                    }

                    yield return remove;
                }
            }
        }
    }
}
