using System;
using System.Reflection;

namespace Loader
{
    public interface IHost : IDisposable
    {
        Assembly GetEntryPoint();

        Assembly Load(string name);
    }
}
