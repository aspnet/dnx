using System;

namespace Microsoft.Framework.Runtime.Json
{
    internal class JsonString : JsonValue
    {
        private readonly string _value;

        public JsonString(string value, JsonToken token)
            : base(token.Position)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _value = value;
        }

        public string Value
        {
            get { return _value; }
        }

        public override string ToString()
        {
            return _value;
        }

        public static implicit operator string (JsonString instance)
        {
            if (instance == null)
            {
                return null;
            }
            else
            {
                return instance.Value;
            }
        }
    }
}
