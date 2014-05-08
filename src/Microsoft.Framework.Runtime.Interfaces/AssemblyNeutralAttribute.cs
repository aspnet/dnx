using System;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class AssemblyNeutralAttribute : Attribute
    {
    }
}
