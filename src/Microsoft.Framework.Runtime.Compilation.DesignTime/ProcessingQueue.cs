using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    internal class ProcessingQueue
    {
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        public event Action<Dictionary<string, int>> ProjectsInitialized;
        public event Action<int, CompileResponse> ProjectCompiled;
        public event Action<int> ProjectChanged;
        public event Action Closed;
        public event Action<IEnumerable<string>> ProjectSources;
        public event Action<int?, string> Error;

        public ProcessingQueue(Stream stream)
        {
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
        }

        public void Start()
        {
            new Thread(ReceiveMessages).Start();
        }

        public void Send(DesignTimeMessage message)
        {
            lock (_writer)
            {
                var obj = new JObject();
                obj["ContextId"] = message.ContextId;
                obj["HostId"] = message.HostId;
                obj["MessageType"] = message.MessageType;
                obj["Payload"] = message.Payload;
                _writer.Write(obj.ToString(Formatting.None));
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    var metadata = _reader.ReadString();
                    var obj = JObject.Parse(metadata);

                    var messageType = obj.Value<string>("MessageType");
                    switch (messageType)
                    {
                        case "Assembly":
                            //{
                            //    "MessageType": "Assembly",
                            //    "ContextId": 1,
                            //    "AssemblyPath": null,
                            //    "Diagnostics": [],
                            //    "Blobs": 2
                            //}
                            // Embedded Refs (special)
                            // Blob 1
                            // Blob 2
                            var compileResponse = new CompileResponse();
                            compileResponse.AssemblyPath = obj.Value<string>(nameof(CompileResponse.AssemblyPath));
                            compileResponse.Diagnostics = ValueAsCompilationMessages(obj, (nameof(CompileResponse.Diagnostics)));
                            int contextId = obj.Value<int>("ContextId");
                            int blobs = obj.Value<int>("Blobs");

                            var embeddedReferencesCount = _reader.ReadInt32();
                            compileResponse.EmbeddedReferences = new Dictionary<string, byte[]>();
                            for (int i = 0; i < embeddedReferencesCount; i++)
                            {
                                var key = _reader.ReadString();
                                int valueLength = _reader.ReadInt32();
                                var value = _reader.ReadBytes(valueLength);
                                compileResponse.EmbeddedReferences[key] = value;
                            }

                            var assemblyBytesLength = _reader.ReadInt32();
                            compileResponse.AssemblyBytes = _reader.ReadBytes(assemblyBytesLength);
                            var pdbBytesLength = _reader.ReadInt32();
                            compileResponse.PdbBytes = _reader.ReadBytes(pdbBytesLength);

                            // Skip over blobs that aren't understood
                            for (int i = 0; i < blobs - 2; i++)
                            {
                                int length = _reader.ReadInt32();
                                _reader.ReadBytes(length);
                            }

                            ProjectCompiled(contextId, compileResponse);

                            break;
                        case "Sources":
                            //{
                            //    "MessageType": "Sources",
                            //    "Files": [],
                            //}
                            var files = obj.ValueAsArray<string>("Files");
                            ProjectSources(files);
                            break;
                        case "ProjectContexts":
                            //{
                            //    "MessageType": "ProjectContexts",
                            //    "Projects": { "path": id },
                            //}
                            var projects = obj["Projects"] as JObject;
                            var projectContexts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            foreach (var entry in projects)
                            {
                                projectContexts[entry.Key] = entry.Value.Value<int>();
                            }

                            ProjectsInitialized(projectContexts);
                            break;
                        case "ProjectChanged":
                            {
                                //{
                                //    "MessageType": "ProjectChanged",
                                //    "ContextId": id,
                                //}
                                int id = obj.Value<int>("ContextId");
                                ProjectChanged(id);
                            }
                            break;
                        case "Error":
                            //{
                            //    "MessageType": "Error",
                            //    "ContextId": id,
                            //    "Payload": {
                            //        "Message": "",
                            //        "Path": "",
                            //        "Line": 0,
                            //        "Column": 1
                            //    }
                            //}
                            {
                                var id = obj["ContextId"]?.Value<int>();
                                var message = obj["Payload"].Value<string>("Message");

                                Error(id, message);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.TraceError("[{0}]: Exception occurred: {1}", GetType().Name, ex);
                Closed();
                return;
            }
        }

        private static List<CompilationMessage> ValueAsCompilationMessages(JObject obj, string key)
        {
            var arrayValue = obj.Value<JArray>(key);
            return arrayValue.Select(item => new CompilationMessage
            {
                Message = item.Value<string>(nameof(ICompilationMessage.Message)),
                FormattedMessage = item.Value<string>(nameof(ICompilationMessage.FormattedMessage)),
                SourceFilePath = item.Value<string>(nameof(ICompilationMessage.SourceFilePath)),
                Severity = (CompilationMessageSeverity)item.Value<int>(nameof(ICompilationMessage.Severity)),
                StartColumn = item.Value<int>(nameof(ICompilationMessage.StartColumn)),
                StartLine = item.Value<int>(nameof(ICompilationMessage.StartLine)),
                EndColumn = item.Value<int>(nameof(ICompilationMessage.EndColumn)),
                EndLine = item.Value<int>(nameof(ICompilationMessage.EndLine)),
            }).ToList();
        }
    }
}