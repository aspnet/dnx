using System;
using Microsoft.Extensions.CompilationAbstractions.Caching;

namespace Microsoft.Dnx.Compilation.Caching
{
    public class CacheContextAccessor : ICacheContextAccessor
    {
        [ThreadStatic]
        private static CacheContext _threadInstance;

        public static CacheContext ThreadInstance
        {
            get { return _threadInstance; }
            set { _threadInstance = value; }
        }

        public CacheContext Current
        {
            get { return ThreadInstance; }
            set { ThreadInstance = value; }
        }
    }
}