using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

namespace Microsoft.Net.Runtime.Roslyn
{
    public class TypeCompilationContext
    {
        public TypeCompilationContext(AssemblyNeutralWorker worker, ITypeSymbol typeSymbol)
        {
            Worker = worker;
            TypeSymbol = typeSymbol;
            Requires = new Dictionary<TypeCompilationContext, AssemblyNeutralWorker.OrderingState.StrengthKind>();
        }

        public AssemblyNeutralWorker Worker { get; private set; }
        public ITypeSymbol TypeSymbol { get; private set; }

        public IDictionary<TypeCompilationContext, AssemblyNeutralWorker.OrderingState.StrengthKind> Requires { get; set; }

        public string AssemblyName
        {
            get
            {
                // TODO: Fix this to generate proper names (seems that the generic type's arity isn't captured here)
                return TypeSymbol.ContainingNamespace + "." + TypeSymbol.Name;
            }
        }

        public CSharpCompilation Compilation { get; private set; }

        public Stream OutputStream { get; private set; }
        public MetadataReference Reference { get; private set; }
        public EmitResult EmitResult { get; private set; }

        public CSharpCompilation ShallowCompilation { get; private set; }
        public Stream ShallowOutputStream { get; private set; }
        public MetadataReference ShallowReference { get; private set; }
        public EmitResult ShallowEmitResult { get; private set; }


        public void SymbolUsage(Action<ISymbol> shallowUsage, Action<ISymbol> deepUsage)
        {
            DoTypeSymbol(deepUsage, TypeSymbol.BaseType);

            foreach (var symbol in TypeSymbol.AllInterfaces)
            {
                DoTypeSymbol(deepUsage, symbol);
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
                    DoTypeSymbol(shallowUsage, propertyMember.Type);
                }
                
                if (fieldMember != null)
                {
                    DoTypeSymbol(shallowUsage, fieldMember.Type);
                }

                if (methodMember != null)
                {
                    DoTypeSymbol(shallowUsage, methodMember.ReturnType);
                    AttributeDataSymbolUsage(methodMember.GetReturnTypeAttributes(), deepUsage);
                    foreach (var parameter in methodMember.Parameters)
                    {
                        DoTypeSymbol(shallowUsage, parameter.Type);
                        AttributeDataSymbolUsage(parameter.GetAttributes(), deepUsage);
                    }
                }
            }
        }

        private void DoTypeSymbol(Action<ISymbol> usage, ITypeSymbol typeSymbol)
        {
            usage(typeSymbol);

            var namedTypeSymbol = typeSymbol as INamedTypeSymbol;
            if (namedTypeSymbol != null)
            {
                usage(namedTypeSymbol.OriginalDefinition);

                foreach (var arg in namedTypeSymbol.TypeArguments)
                {
                    if (arg is ITypeParameterSymbol)
                    {
                        // Unspecified type paramters can be skipped
                        continue;
                    }

                    // TODO: Stack guard (or just use a stack)
                    DoTypeSymbol(usage, arg);
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
                DoTypeSymbol(deepUsage, argument.Type);
            }

            foreach (var argument in attributeData.NamedArguments)
            {
                DoTypeSymbol(deepUsage, argument.Value.Type);
            }
        }

        public EmitResult Generate()
        {
            Compilation = CSharpCompilation.Create(
                assemblyName: AssemblyName,
                options: Worker.OriginalCompilation.Options,
                references: Worker.OriginalCompilation.References);

            foreach (var other in Requires.Keys)
            {
                if (other.EmitResult != null && !other.EmitResult.Success)
                {
                    // Skip this reference if it hasn't beed emitted
                    continue;
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
                options: Worker.OriginalCompilation.Options,
                references: Worker.OriginalCompilation.References);

            foreach (var other in Requires)
            {
                if (other.Value == AssemblyNeutralWorker.OrderingState.StrengthKind.DeepUsage)
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
