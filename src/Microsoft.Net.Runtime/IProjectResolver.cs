
namespace Microsoft.Net.Runtime
{
    public interface IProjectResolver
    {
        bool TryResolveProject(string name, out Project project);
    }
}
