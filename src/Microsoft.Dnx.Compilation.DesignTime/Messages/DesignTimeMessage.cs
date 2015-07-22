namespace Microsoft.Dnx.Runtime
{
    internal class DesignTimeMessage
    {
        public string HostId { get; set; } = "Application";
        public string MessageType { get; set; }
        public int ContextId { get; set; }
        public int Version { get; set; } = 1;

        public string ToJsonString()
        {
            // TODO: Consider removing the whitespace
            return $@"
{{ 
    ""HostId"": ""{HostId}"", 
    ""MessageType"": ""{MessageType}"",
    ""ContextId"": {ContextId},
    ""Payload"": {{
        ""Version"": {Version}
        {FormatPayload()}
    }}
}}";
        }

        private string FormatPayload()
        {
            var payload = GetPayload();

            return string.IsNullOrEmpty(payload) ? "" : "," + payload;
        }

        protected virtual string GetPayload()
        {
            return null;
        }
    }
}