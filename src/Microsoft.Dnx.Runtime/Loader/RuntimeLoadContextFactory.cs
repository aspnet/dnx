using System;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Runtime.Loader
{
    internal class RuntimeLoadContextFactory : IAssemblyLoadContextFactory
    {
        private readonly IAssemblyLoadContextAccessor _accessor;

        public RuntimeLoadContextFactory(IServiceProvider serviceProvider)
        {
            _accessor = (IAssemblyLoadContextAccessor)serviceProvider.GetService(typeof(IAssemblyLoadContextAccessor));
        }

        public IAssemblyLoadContext Create(IServiceProvider serviceProvider)
        {
            return _accessor.Default;
        }
    }
}