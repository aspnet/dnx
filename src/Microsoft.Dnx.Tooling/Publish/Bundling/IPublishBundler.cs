namespace Microsoft.Dnx.Tooling.Publish.Bundling
{
    public interface IPublishBundler
    {
        bool Bundle(Runtime.Project project, PublishRoot publishRoot, Reports reports);
    }
}