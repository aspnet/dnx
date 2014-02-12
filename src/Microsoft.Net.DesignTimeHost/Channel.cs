using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Net.Runtime.DesignTimeHost
{
    public class Channel : IDisposable
    {
        private int _id;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly Stream _stream;

        private readonly Dictionary<long, Action<Response>> _invocations = new Dictionary<long, Action<Response>>();
        private readonly Dictionary<string, Func<Request, Response>> _callbacks = new Dictionary<string, Func<Request, Response>>(StringComparer.OrdinalIgnoreCase);

        private bool _isBound;

        public Channel(Stream stream)
        {
            _stream = stream;
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);

            new Thread(() => ReadLoop()).Start();
        }

        public Task Invoke(string name, params object[] args)
        {
            return Invoke<object>(name, args);
        }

        public Task<T> Invoke<T>(string name, params object[] args)
        {
            int id = Interlocked.Increment(ref _id);

            var request = new Request
            {
                Id = id,
                Method = name,
                Args = args.Select(a => JToken.FromObject(a)).ToArray()
            };

            var tcs = new TaskCompletionSource<T>();

            lock (_invocations)
            {
                _invocations[id] = response =>
                {
                    // If there's no response then cancel the call
                    if (response == null)
                    {
                        tcs.SetCanceled();
                    }
                    else if (response.Error != null)
                    {
                        tcs.SetException(new InvalidOperationException(response.Error));
                    }
                    else
                    {
                        tcs.SetResult(response.Result.ToObject<T>());
                    }
                };
            }

            Write(MessageType.Request, request);

            return tcs.Task;
        }

        public IDisposable Bind(object value)
        {
            if (_isBound)
            {
                throw new NotSupportedException("Can't bind to different objects");
            }

            _isBound = true;

            var methods = new List<string>();

            foreach (var m in value.GetType().GetTypeInfo().DeclaredMethods.Where(m => m.IsPublic))
            {
                methods.Add(m.Name);

                var parameters = m.GetParameters();

                if (_callbacks.ContainsKey(m.Name))
                {
                    throw new NotSupportedException(String.Format("Duplicate definitions of {0}. Overloading is not supported.", m.Name));
                }

                _callbacks[m.Name] = request =>
                {
                    var response = new Response();
                    response.Id = request.Id;

                    try
                    {
                        var args = request.Args.Zip(parameters, (a, p) => a.ToObject(p.ParameterType))
                                               .ToArray();

                        var result = m.Invoke(value, args);

                        if (result != null)
                        {
                            response.Result = JToken.FromObject(result);
                        }
                    }
                    catch (TargetInvocationException ex)
                    {
                        response.Error = ex.InnerException.Message;
                    }
                    catch (Exception ex)
                    {
                        response.Error = ex.Message;
                    }

                    return response;
                };
            }

            return new DisposableAction(() =>
            {
                foreach (var m in methods)
                {
                    lock (_callbacks)
                    {
                        _callbacks.Remove(m);
                    }
                }
            });
        }

        private void ReadLoop()
        {
            try
            {
                while (true)
                {
                    var type = (MessageType)_reader.ReadByte();
                    var data = _reader.ReadString();

                    if (type == MessageType.Request)
                    {
                        var request = JsonConvert.DeserializeObject<Request>(data);

                        Response response = null;

                        Func<Request, Response> callback;
                        if (_callbacks.TryGetValue(request.Method, out callback))
                        {
                            response = callback(request);
                        }
                        else
                        {
                            // If there's no method then return a failed response for this request
                            response = new Response
                            {
                                Id = request.Id,
                                Error = String.Format("Unknown method '{0}'", request.Method)
                            };
                        }

                        Write(MessageType.Response, response);
                    }
                    else if (type == MessageType.Response)
                    {
                        var response = JsonConvert.DeserializeObject<Response>(data);

                        lock (_invocations)
                        {
                            Action<Response> invocation;
                            if (_invocations.TryGetValue(response.Id, out invocation))
                            {
                                invocation(response);

                                _invocations.Remove(response.Id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                // Any pending callbacks need to be cleaned up
                lock (_invocations)
                {
                    foreach (var invocation in _invocations)
                    {
                        invocation.Value(null);
                    }
                }
            }
        }

        private void Write(MessageType type, object value)
        {
            var data = JsonConvert.SerializeObject(value);

            _writer.Write((byte)type);
            _writer.Write(data);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private class DisposableAction : IDisposable
        {
            private Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _action, () => { }).Invoke();
            }
        }
    }
}
