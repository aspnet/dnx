using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public sealed class FileFormatException : Exception
    {
        public FileFormatException(string message) :
            base(message)
        {
        }

        public FileFormatException(string message, Exception innerException) :
            base(message, innerException)
        {

        }

        public string Path { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        private FileFormatException WithLineInfo(IJsonLineInfo lineInfo)
        {
            if (lineInfo != null)
            {
                Line = lineInfo.LineNumber;
                Column = lineInfo.LinePosition;
            }

            return this;
        }

        public static FileFormatException Create(Exception exception, JToken value, string path)
        {
            var lineInfo = (IJsonLineInfo)value;

            return new FileFormatException(exception.Message, exception)
            {
                Path = path
            }
            .WithLineInfo(lineInfo);
        }

        public static FileFormatException Create(string message, JToken value, string path)
        {
            var lineInfo = (IJsonLineInfo)value;

            return new FileFormatException(message)
            {
                Path = path
            }
            .WithLineInfo(lineInfo);
        }

        internal static FileFormatException Create(JsonReaderException exception, string path)
        {
            return new FileFormatException(exception.Message, exception)
            {
                Path = path,
                Column = exception.LinePosition,
                Line = exception.LineNumber
            };
        }
    }
}