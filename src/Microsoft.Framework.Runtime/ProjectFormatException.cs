using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public sealed class ProjectFormatException : Exception
    {
        public ProjectFormatException(string message) :
            base(message)
        {
        }

        public ProjectFormatException(string message, Exception innerException) :
            base(message, innerException)
        {

        }

        public string Path { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        private ProjectFormatException WithLineInfo(IJsonLineInfo lineInfo)
        {
            Line = lineInfo.LineNumber;
            Column = lineInfo.LinePosition;

            return this;
        }

        public static ProjectFormatException Create(Exception exception, JToken value, string path)
        {
            var lineInfo = (IJsonLineInfo)value;

            return new ProjectFormatException(exception.Message, exception)
            {
                Path = path
            }
            .WithLineInfo(lineInfo);
        }

        public static ProjectFormatException Create(string message, JToken value, string path)
        {
            var lineInfo = (IJsonLineInfo)value;

            return new ProjectFormatException(message)
            {
                Path = path
            }
            .WithLineInfo(lineInfo);
        }

        internal static ProjectFormatException Create(JsonReaderException exception, string path)
        {
            return new ProjectFormatException(exception.Message, exception)
            {
                Path = path,
                Column = exception.LinePosition,
                Line = exception.LineNumber
            };
        }
    }
}