using System;

namespace Microsoft.Framework.Runtime.Loader
{
    internal class RuntimeLoadContextFactory : IAssemblyLoadContextFactory
    {
        private readonly IAssemblyLoadContextAccessor _accessor;

        public RuntimeLoadContextFactory(IServiceProvider serviceProvider)
            : this((IAssemblyLoadContextAccessor)serviceProvider.GetService(typeof(IAssemblyLoadContextAccessor)))
        {
        }

        public RuntimeLoadContextFactory(IAssemblyLoadContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public IAssemblyLoadContext Create()
        {
            return _accessor.Default;
        }
    }
}