// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Dnx.Runtime.Internal
{
    public static class ConcurrencyUtilities
    {
        internal static string FilePathToLockName(string filePath)
        {
            // If we use a file path directly as the name of a semaphore,
            // the ctor of semaphore looks for the file and throws an IOException
            // when the file doesn't exist. So we need a conversion from a file path
            // to a unique lock name.
            return $"DNU_RESTORE_{filePath.Replace(Path.DirectorySeparatorChar, '_')}";
        }

        public static void ExecuteWithFileLocked(string filePath, Action<bool> action)
        {
            ExecuteWithFileLocked(filePath, createdNew =>
            {
                action(createdNew);
                return Task.FromResult(1);
            })
            .GetAwaiter().GetResult();
        }

        public async static Task<T> ExecuteWithFileLocked<T>(string filePath, Func<bool, Task<T>> action)
        {
            bool completed = false;
            while (!completed)
            {
                var createdNew = false;
                using (var fileLock = SemaphoreWrapper.Create(initialCount: 0, maximumCount: 1, name: FilePathToLockName(filePath),
                    createdNew: out createdNew))
                {
                    try
                    {
                        // If this lock is already acquired by another process, wait until we can acquire it
                        if (!createdNew)
                        {
                            var signaled = fileLock.WaitOne(TimeSpan.FromSeconds(5));
                            if (!signaled)
                            {
                                // Timeout and retry
                                continue;
                            }
                        }

                        completed = true;
                        return await action(createdNew);
                    }
                    finally
                    {
                        if (completed)
                        {
                            fileLock.Release();
                        }
                    }
                }
            }

            // should never get here
            throw new TaskCanceledException($"Failed to acquire semaphore for file: {filePath}");
        }

        private class SemaphoreWrapper : IDisposable
        {
#if DNXCORE50
            private static Dictionary<string, SemaphoreWrapper> _nameWrapper =
                new Dictionary<string, SemaphoreWrapper>();

            private readonly string _name;
            private volatile int _refCount = 0;
#endif

            private readonly Semaphore _semaphore;

            public static SemaphoreWrapper Create(int initialCount, int maximumCount, string name, out bool createdNew)
            {
#if DNXCORE50
                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    return new SemaphoreWrapper(new Semaphore(initialCount, maximumCount, name, out createdNew));
                }
                else
                {
                    var createdNewLocal = false;
                    SemaphoreWrapper wrapper;

                    lock (_nameWrapper)
                    {
                        wrapper = _nameWrapper.GetOrAdd(
                            name,
                            _ =>
                            {
                                createdNewLocal = true;
                                return new SemaphoreWrapper(new Semaphore(initialCount, maximumCount), name);
                            });
                        wrapper._refCount++;
                    }

                    // C# doesn't allow assigning value to an out parameter directly in lambda expression
                    createdNew = createdNewLocal;
                    return wrapper;
                }
#else

                return new SemaphoreWrapper(new Semaphore(initialCount, maximumCount, name, out createdNew));
#endif
            }

            private SemaphoreWrapper(Semaphore semaphore, string name = null)
            {
                _semaphore = semaphore;
#if DNXCORE50
                _name = name;
#endif
            }

            public bool WaitOne(TimeSpan timeout)
            {
                return _semaphore.WaitOne(timeout);
            }

            public int Release()
            {
                return _semaphore.Release();
            }

            public void Dispose()
            {
#if DNXCORE50
                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    _semaphore.Dispose();
                }
                else
                {
                    lock (_nameWrapper)
                    {
                        _refCount--;
                        if (_refCount == 0)
                        {
                            _nameWrapper.Remove(_name);
                            _semaphore.Dispose();
                        }
                    }

                }
#else
                _semaphore.Dispose();
#endif
            }
        }
    }
}