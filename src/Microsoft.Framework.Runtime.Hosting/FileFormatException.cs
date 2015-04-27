// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime.Json;

namespace Microsoft.Framework.Runtime
{
    public sealed class FileFormatException : Exception
    {
        private FileFormatException(string message) :
            base(message)
        {
        }

        private FileFormatException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        public string Path { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        internal static FileFormatException Create(Exception exception, string filePath)
        {
            if (exception is JsonDeserializerException)
            {
                return new FileFormatException(exception.Message, exception)
                   .WithFilePath(filePath)
                   .WithLineInfo((JsonDeserializerException)exception);
            }
            else
            {
                return new FileFormatException(exception.Message, exception)
                    .WithFilePath(filePath);
            }
        }

        internal static FileFormatException Create(Exception exception, JsonValue jsonValue, string filePath)
        {
            var result = new FileFormatException(exception.Message, exception)
                .WithFilePath(filePath)
                .WithLineInfo(jsonValue.Position);

            return result;
        }

        internal static FileFormatException Create(string message, string filePath)
        {
            var result = new FileFormatException(message)
                .WithFilePath(filePath);

            return result;
        }

        internal static FileFormatException Create(string message, JsonValue jsonValue, string filePath)
        {
            var result = new FileFormatException(message)
                .WithFilePath(filePath)
                .WithLineInfo(jsonValue.Position);

            return result;
        }

        private FileFormatException WithLineInfo(JsonPosition position)
        {
            if (position == null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            Line = position.Line;
            Column = position.Column;

            return this;
        }

        private FileFormatException WithLineInfo(JsonDeserializerException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            Line = exception.Line;
            Column = exception.Column;

            return this;
        }

        private FileFormatException WithFilePath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Path = path;

            return this;
        }
    }
}