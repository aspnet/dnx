// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class CompilationContext
    {
        private readonly BeforeCompileContext _beforeCompileContext;
        private readonly Func<IList<ResourceDescription>> _resourcesResolver;

        public IList<IMetadataReference> References { get; set; }

        public CompilationContext(CSharpCompilation compilation,
                                  CompilationProjectContext compilationContext,
                                  IEnumerable<IMetadataReference> incomingReferences,
                                  Func<IList<ResourceDescription>> resourcesResolver)
        {
            Project = compilationContext;
            Modules = new List<ICompileModule>();

            _resourcesResolver = resourcesResolver;

            var projectContext = new ProjectContext
            {
                Name = compilationContext.Target.Name,
                ProjectDirectory = compilationContext.ProjectDirectory,
                ProjectFilePath = compilationContext.ProjectFilePath,
                TargetFramework = compilationContext.Target.TargetFramework,
                Version = compilationContext.Version?.ToString(),
                Configuration = compilationContext.Target.Configuration
            };

            _beforeCompileContext = new BeforeCompileContext
            {
                Compilation = compilation,
                ProjectContext = projectContext,
                Resources = new LazyList<ResourceDescription>(ResolveResources),
                Diagnostics = new List<Diagnostic>(),
                MetadataReferences = new List<IMetadataReference>(incomingReferences)
            };
        }

        public CompilationProjectContext Project { get; }

        public IList<ICompileModule> Modules { get; }

        public CSharpCompilation Compilation
        {
            get { return _beforeCompileContext.Compilation; }
            set { _beforeCompileContext.Compilation = value; }
        }

        public IList<Diagnostic> Diagnostics
        {
            get { return _beforeCompileContext.Diagnostics; }
        }

        public IList<ResourceDescription> Resources
        {
            get { return _beforeCompileContext.Resources; }
        }

        public ProjectContext ProjectContext
        {
            get { return _beforeCompileContext.ProjectContext; }
        }

        public BeforeCompileContext BeforeCompileContext
        {
            get { return _beforeCompileContext; }
        }

        private IList<ResourceDescription> ResolveResources()
        {
            var sw = Stopwatch.StartNew();
            Logger.TraceInformation("[{0}]: Generating resources for {1}", nameof(CompilationContext), Project.Target.Name);

            var resources = _resourcesResolver();

            sw.Stop();
            Logger.TraceInformation("[{0}]: Generated resources for {1} in {2}ms", nameof(CompilationContext),
                                                                                   Project.Target.Name,
                                                                                   sw.ElapsedMilliseconds);

            return resources;
        }

        private class LazyList<T> : IList<T>
        {
            private Lazy<IList<T>> _list;

            public LazyList(Func<IList<T>> initializer)
            {
                _list = new Lazy<IList<T>>(initializer);
            }

            public T this[int index]
            {
                get { return _list.Value[index]; }
                set { _list.Value[index] = value; }
            }

            public int Count
            {
                get { return _list.Value.Count; }
            }

            public bool IsReadOnly
            {
                get { return _list.Value.IsReadOnly; }
            }

            public void Add(T item)
            {
                _list.Value.Add(item);
            }

            public void Clear()
            {
                _list.Value.Clear();
            }

            public bool Contains(T item)
            {
                return _list.Value.Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                _list.Value.CopyTo(array, arrayIndex);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return _list.Value.GetEnumerator();
            }

            public int IndexOf(T item)
            {
                return _list.Value.IndexOf(item);
            }

            public void Insert(int index, T item)
            {
                _list.Value.Insert(index, item);
            }

            public bool Remove(T item)
            {
                return _list.Value.Remove(item);
            }

            public void RemoveAt(int index)
            {
                _list.Value.RemoveAt(index);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _list.Value.GetEnumerator();
            }
        }
    }
}
