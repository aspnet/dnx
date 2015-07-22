// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation.DesignTime
{
    public class DesignTimeHostProjectCompiler : IProjectCompiler
    {
        private readonly IDesignTimeHostCompiler _compiler;

        public DesignTimeHostProjectCompiler(IApplicationShutdown shutdown, IFileWatcher watcher, RuntimeOptions runtimeOptions)
        {
            // Using this ctor because it works on mono, this is hard coded to ipv4
            // right now. Mono will eventually have the dualmode overload
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(IPAddress.Loopback, runtimeOptions.CompilationServerPort.Value));

            var networkStream = new NetworkStream(socket);

            _compiler = new DesignTimeHostCompiler(shutdown, watcher, networkStream);
        }

        public IMetadataProjectReference CompileProject(
            CompilationProjectContext projectContext,
            Func<LibraryExport> referenceResolver,
            Func<IList<ResourceDescriptor>> resourcesResolver)
        {
            // The target framework and configuration are assumed to be correct
            // in the design time process
            var task = _compiler.Compile(projectContext.ProjectDirectory, projectContext.Target);

            return new DesignTimeProjectReference(projectContext, task.Result);
        }
    }
}
