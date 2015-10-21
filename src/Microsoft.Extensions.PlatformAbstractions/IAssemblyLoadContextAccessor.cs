using System.Reflection;

namespace Microsoft.Extensions.PlatformAbstractions
{
    /// <summary>
    /// Provides access to created <see cref="IAssemblyLoadContext"/>
    /// </summary>
    public interface IAssemblyLoadContextAccessor
    {
        /// <summary>
        /// Gets the default <see cref="IAssemblyLoadContext"/>.
        /// </summary>
        IAssemblyLoadContext Default { get; }

        /// <summary>
        /// Gets the <see cref="IAssemblyLoadContext"/> associated with the specified <see cref="Assembly"/>.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        IAssemblyLoadContext GetLoadContext(Assembly assembly);
    }
}