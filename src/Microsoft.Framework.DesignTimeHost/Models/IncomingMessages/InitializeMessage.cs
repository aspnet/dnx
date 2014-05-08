namespace Microsoft.Framework.DesignTimeHost.Models.IncomingMessages
{
    public class InitializeMessage
    {
        public string TargetFramework { get; set; }
        public string ProjectFolder { get; set; }
    }
}