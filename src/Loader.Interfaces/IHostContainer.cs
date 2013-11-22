
using System;
using System.Reflection;

namespace Loader
{
    public interface IHostContainer
    {
        IDisposable AddHost(IHost host);

        Assembly GetEntryPoint();
    }
}
