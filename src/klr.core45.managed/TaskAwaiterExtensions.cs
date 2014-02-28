using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class TaskAwaiterExtensions
{
    /// <summary>
    /// Turns a managed TaskAwaiter&lt;int&gt; into a COM awaiter object.
    /// </summary>
    public static IAsyncAwaiter ToComAwaiter(this TaskAwaiter<int> awaiter)
    {
        return new ComAwaiter(awaiter);
    }

    private sealed class ComAwaiter : IAsyncAwaiter
    {
        private TaskAwaiter<int> _awaiter;

        internal ComAwaiter(TaskAwaiter<int> awaiter)
        {
            _awaiter = awaiter;
        }

        public bool IsCompleted
        {
            get { return _awaiter.IsCompleted; }
        }

        public int GetResult()
        {
            return _awaiter.GetResult();
        }

        public void OnCompleted(IntPtr pfnContinuation, IntPtr vState)
        {
            if (pfnContinuation == null)
            {
                throw new ArgumentNullException("pfnContinuation");
            }

            var continuationFunction = Marshal.GetDelegateForFunctionPointer<AwaitContinuation>(pfnContinuation);
            _awaiter.UnsafeOnCompleted(() => continuationFunction(vState));
        }
    }
}
