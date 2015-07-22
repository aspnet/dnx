namespace Microsoft.Dnx.Runtime
{
    internal class EnumerateProjectContextsMessage : DesignTimeMessage
    {
        public EnumerateProjectContextsMessage()
        {
            MessageType = "EnumerateProjectContexts";
        }
    }
}
