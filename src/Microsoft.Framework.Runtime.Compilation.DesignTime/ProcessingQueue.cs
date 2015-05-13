using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Framework.Runtime.Json;

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
                var json = message.ToJsonString();
                _writer.Write(json);
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    var metadata = _reader.ReadString();
                    var obj = JsonDeserializer.Deserialize(new StringReader(metadata)) as JsonObject;

                    var messageType = obj.ValueAsString("MessageType");
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
                            compileResponse.AssemblyPath = obj.ValueAsString(nameof(CompileResponse.AssemblyPath));
                            compileResponse.Diagnostics = ValueAsCompilationMessages(obj, (nameof(CompileResponse.Diagnostics)));
                            int contextId = obj.ValueAsInt("ContextId");
                            int blobs = obj.ValueAsInt("Blobs");

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
                            var files = obj.ValueAsStringArray("Files");
                            ProjectSources(files);
                            break;
                        case "ProjectContexts":
                            //{
                            //    "MessageType": "ProjectContexts",
                            //    "Projects": { "path": id },
                            //}
                            var projects = obj.ValueAsJsonObject("Projects");
                            var projectContexts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            foreach (var key in projects.Keys)
                            {
                                projectContexts[key] = projects.ValueAsInt(key);
                            }

                            ProjectsInitialized(projectContexts);
                            break;
                        case "ProjectChanged":
                            {
                                //{
                                //    "MessageType": "ProjectChanged",
                                //    "ContextId": id,
                                //}
                                int id = obj.ValueAsInt("ContextId");
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
                                var id = obj.ValueAsInt("ContextId");
                                var message = obj.ValueAsJsonObject("Payload").
                                                  ValueAsString("Message");

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

        private static List<CompilationMessage> ValueAsCompilationMessages(JsonObject obj, string key)
        {
            var messages = new List<CompilationMessage>();

            var arrayValue = obj.Value(key) as JsonArray;
            for (int i = 0; i < arrayValue.Length; i++)
            {
                var item = arrayValue[i] as JsonObject;

                var message = new CompilationMessage
                {
                    Message = item.ValueAsString(nameof(ICompilationMessage.Message)),
                    FormattedMessage = item.ValueAsString(nameof(ICompilationMessage.FormattedMessage)),
                    SourceFilePath = item.ValueAsString(nameof(ICompilationMessage.SourceFilePath)),
                    Severity = (CompilationMessageSeverity)item.ValueAsInt(nameof(ICompilationMessage.Severity)),
                    StartColumn = item.ValueAsInt(nameof(ICompilationMessage.StartColumn)),
                    StartLine = item.ValueAsInt(nameof(ICompilationMessage.StartLine)),
                    EndColumn = item.ValueAsInt(nameof(ICompilationMessage.EndColumn)),
                    EndLine = item.ValueAsInt(nameof(ICompilationMessage.EndLine)),
                };

                messages.Add(message);
            }

            return messages;
        }
    }
}