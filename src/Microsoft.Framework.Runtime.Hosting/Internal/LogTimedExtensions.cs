using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.Framework.Logging
{
    public static class LogTimedExtensions
    {
        public static IDisposable LogTimedMethod(this ILogger self, [CallerMemberName] string method = null)
        {
            return LogTimed(self, method);
        }

        public static IDisposable LogTimed(this ILogger self, string activity)
        {
            if (self.IsEnabled(LogLevel.Debug))
            {
                self.LogDebug("Started " + activity);
                Stopwatch sw = new Stopwatch();
                var ret = new DisposableAction(() =>
                {
                    sw.Stop();
                    self.LogDebug($"Finished {activity} in {sw.ElapsedMilliseconds:0.00}ms");
                });
                sw.Start();
                return ret;
            }
            else
            {
                return DisposableAction.Null;
            }
        }

        private class DisposableAction : IDisposable
        {
            public static readonly DisposableAction Null = new DisposableAction(null);
            private readonly Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                if (_action != null)
                {
                    _action();
                }
            }
        }
    }
}