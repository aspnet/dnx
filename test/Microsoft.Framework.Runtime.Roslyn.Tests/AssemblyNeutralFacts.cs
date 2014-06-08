// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.Framework.Runtime.Roslyn.Tests
{
    public class AssemblyNeutralFacts
    {
        [Fact]
        public void TypeCompilationsAreGeneratedForEachAssemblyNeutralType()
        {
            var worker = DoAssemblyNeutralCompilation(@"
namespace Something
{
    [AssemblyNeutral]
    public interface IFoo { }

    [AssemblyNeutral]
    public interface IBar { }

    [AssemblyNeutral]
    public class AssemblyNeutralAttribute : System.Attribute { }
}
");

            var diagnostics = worker.GenerateTypeCompilations();
            var compilations = worker.TypeCompilations.OrderBy(c => c.AssemblyName).ToList();

            Assert.Equal(0, diagnostics.Count);
            Assert.Equal(3, compilations.Count());
            Assert.Equal("Something.AssemblyNeutralAttribute", compilations[0].AssemblyName);
            Assert.Equal("Something.IBar", compilations[1].AssemblyName);
            Assert.Equal("Something.IFoo", compilations[2].AssemblyName);
        }

        [Fact]
        public void CircularReferencesInMembers()
        {
            var worker = DoAssemblyNeutralCompilation(
@"
namespace Something
{
    [AssemblyNeutral]
    public interface IHttpRequest 
    { 
        IHttpContext Context { get; }

        string Verb { get; } 
    }
}",
@"
namespace Something
{
    [AssemblyNeutral]
    public interface IHttpResponse 
    { 
        IHttpContext Context { get; }

        void Write(string text);
    }
}",
@"
namespace Something
{
    [AssemblyNeutral]
    public interface IHttpContext 
    { 
        IHttpRequest Request { get; set; }
        IHttpResponse Response { get; set; }
    }
}",
@"
namespace Something
{
    [AssemblyNeutral]
    public class AssemblyNeutralAttribute : System.Attribute { }
}
");

            var diagnostics = worker.GenerateTypeCompilations();
            var compilations = worker.TypeCompilations.OrderBy(c => c.AssemblyName).ToList();

            Assert.Equal(0, diagnostics.Count);
            Assert.Equal(4, compilations.Count());
            Assert.Equal("Something.AssemblyNeutralAttribute", compilations[0].AssemblyName);
            Assert.Equal("Something.IHttpContext", compilations[1].AssemblyName);
            Assert.Equal("Something.IHttpRequest", compilations[2].AssemblyName);
            Assert.Equal("Something.IHttpResponse", compilations[3].AssemblyName);
        }

        [Fact]
        public void GenericMembersWithAssemblyNeutralTypeParams()
        {
            var worker = DoAssemblyNeutralCompilation(
@"
namespace Something
{
    [AssemblyNeutral]
    public interface IDataObject { }
}",
@"
using System.Collections.Generic;

namespace Something
{
    [AssemblyNeutral]
    public interface IDataObjectProvider 
    { 
        IList<IDataObject> Data { get; }
    }
}",
@"
namespace Something
{
    [AssemblyNeutral]
    public class AssemblyNeutralAttribute : System.Attribute { }
}
");

            var diagnostics = worker.GenerateTypeCompilations();
            var compilations = worker.TypeCompilations.OrderBy(c => c.AssemblyName).ToList();

            Assert.Equal(0, diagnostics.Count);
            Assert.Equal(3, compilations.Count());
            Assert.Equal("Something.AssemblyNeutralAttribute", compilations[0].AssemblyName);
            Assert.Equal("Something.IDataObject", compilations[1].AssemblyName);
            Assert.Equal("Something.IDataObjectProvider", compilations[2].AssemblyName);
        }

        [Fact]
        public void GenericAssemblyNeutralMembersWithPrimitiveTypeParams()
        {
            var worker = DoAssemblyNeutralCompilation(
@"
using System.Collections.Generic;

namespace Something
{
    [AssemblyNeutral]
    public interface IDataObject<T> 
    { 
        IList<T> Values { get; } 
    }
}",
@"
using System.Collections.Generic;

namespace Something
{
    [AssemblyNeutral]
    public interface IDataObjectProvider 
    { 
        IList<IDataObject<IDataValue<int>>> Data { get; }
    }
}",
@"
namespace Something
{
    [AssemblyNeutral]
    public interface IDataValue<T> 
    { 
        T GetValue();
    }
}",
@"
namespace Something
{
    [AssemblyNeutral]
    public class AssemblyNeutralAttribute : System.Attribute { }
}
");

            var diagnostics = worker.GenerateTypeCompilations();
            var compilations = worker.TypeCompilations.OrderBy(c => c.AssemblyName).ToList();

            Assert.Equal(0, diagnostics.Count);
            Assert.Equal(4, compilations.Count());
            Assert.Equal("Something.AssemblyNeutralAttribute", compilations[0].AssemblyName);
            Assert.Equal("Something.IDataObject", compilations[1].AssemblyName);
            Assert.Equal("Something.IDataObjectProvider", compilations[2].AssemblyName);
            Assert.Equal("Something.IDataValue", compilations[3].AssemblyName);
        }

        private AssemblyNeutralWorker DoAssemblyNeutralCompilation(params string[] fileContents)
        {
            var compilation = CSharpCompilation.Create("test",
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                references: new[] { 
                    new MetadataFileReference(typeof(object).GetTypeInfo().Assembly.Location) 
                },
                syntaxTrees: fileContents.Select(text => CSharpSyntaxTree.ParseText(text)));

            var worker = new AssemblyNeutralWorker(compilation,
                new Dictionary<string, MetadataReference>());
            worker.FindTypeCompilations(compilation.GlobalNamespace);
            worker.OrderTypeCompilations();
            return worker;
        }
    }
}
