
namespace Microsoft.Net.Runtime.Loader.Roslyn
{
    public interface IProjectResolver
    {
        bool TryResolveProject(string name, out Project project);
    }
}
