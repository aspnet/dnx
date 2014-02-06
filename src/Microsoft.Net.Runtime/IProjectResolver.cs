
namespace Microsoft.Net.Runtime.Loader
{
    public interface IProjectResolver
    {
        bool TryResolveProject(string name, out Project project);
    }
}
