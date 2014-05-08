
namespace Microsoft.Framework.Runtime
{
    public interface IProjectResolver
    {
        bool TryResolveProject(string name, out Project project);
    }
}
