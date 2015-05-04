// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Framework.Runtime.Json
{
    /// <summary>
    /// JsonBuffer represents a piece of loaded json content.
    /// 
    /// The JsonBuffer is used in JsonDeserializer only. It is not inteneded to be
    /// used for data exchange.
    /// </summary>
    internal class JsonContent
    {
        private List<string> _content = new List<string>();

        /// <summary>
        /// Create a JsonContent instance from a stream.
        ///
        /// Once created the JsonContent instance won't keep the handle to the 
        /// stream nor dispose or close it.
        /// </summary>
        /// <param name="stream">Content source</param>
        /// <returns>Newly created JsonContent instance</returns>
        public static JsonContent CreateFromStream(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var content = new List<string>();
            var reader = new StreamReader(stream);

            string line = reader.ReadLine();
            while (line != null)
            {
                content.Add(line);
                line = reader.ReadLine();
            }

            return new JsonContent(content);
        }

        private JsonContent(List<string> content)
        {
            _content = content;
        }

        public int TotalLines { get { return _content.Count; } }

        /// <summary>
        /// Current line number in zero-based index.
        /// </summary>
        public int CurrentLine { get; private set; } = 0;

        /// <summary>
        /// Current column number in zero-based index.
        /// </summary>
        public int CurrentColumn { get; private set; } = -1;

        public char CurrentChar
        {
            get { return _content[CurrentLine][CurrentColumn]; }
        }

        public bool ValidCursor
        {
            get
            {
                return CurrentLine < _content.Count &&
                       CurrentLine >= 0 &&
                       CurrentColumn < _content[CurrentLine].Length &&
                       CurrentColumn >= 0;
            }
        }

        public bool Started
        {
            get { return CurrentLine != 0 || CurrentColumn != -1; }
        }

        /// <summary>
        /// Move the cursor to the next non empty char
        /// </summary>
        /// <returns>Returns false if the cursor reach the end of the content.</returns>
        public bool MoveToNextNonEmptyChar()
        {
            while (TotalLines > CurrentLine)
            {
                while (_content[CurrentLine].Length > CurrentColumn + 1)
                {
                    char c = _content[CurrentLine][++CurrentColumn];
                    if (!char.IsWhiteSpace(c))
                    {
                        return true;
                    }
                }

                CurrentLine++;
                CurrentColumn = -1;
            }

            return false;
        }

        /// <summary>
        /// Move the cursor to the next char
        /// </summary>
        /// <returns>Returns false if the cursor reach the end of the content.</returns>
        public bool MoveNext()
        {
            if (CurrentColumn + 1 < _content[CurrentLine].Length)
            {
                CurrentColumn += 1;
                return true;
            }
            else
            {
                // find the first non empty line after current line
                var targetLine = CurrentLine;
                while (++targetLine < TotalLines && _content[targetLine].Length == 0)
                {
                }

                if (targetLine >= TotalLines)
                {
                    return false;
                }
                else
                {
                    CurrentLine = targetLine;
                    CurrentColumn = 0;
                    return true;
                }
            }
        }

        /// <summary>
        /// Move the cursor to the previous char.
        /// </summary>
        /// <returns>Returns false if the cursor reach it's inital position at [0, -1]</returns>
        public bool MovePrev()
        {
            if (CurrentColumn - 1 >= 0)
            {
                CurrentColumn -= 1;
                return true;
            }
            else if (CurrentLine == 0 && CurrentColumn == 0)
            {
                /// If cursor at [Line:0, Column:0] current then allow it to 
                /// move into the position ahead of it. Therefore it allows the 
                /// first MoveNext or MoveToNextNonEmptyChar to be functional
                CurrentColumn = -1;
                return true;
            }
            else if (CurrentLine == 0 && CurrentColumn == -1)
            {
                return false;
            }
            else
            {
                var targetLine = CurrentLine;

                // find the first non empty line before current line
                while (--targetLine >= 0 && _content[targetLine].Length == 0)
                {
                }

                if (targetLine < 0)
                {
                    // Even the first line is empty, move it to the initial position.
                    CurrentLine = 0;
                    CurrentColumn = -1;
                    return true;
                }
                else
                {
                    CurrentLine = targetLine;
                    CurrentColumn = _content[CurrentLine].Length - 1;
                    return true;
                }
            }
        }

        /// <summary>
        /// Returns a short status message for debugging
        /// </summary>
        public string GetStatusInfo(string message = null)
        {
            return string.Format(@"{0} at [Line: {1}, Column: {2}, Char: {3}]",
                message ?? "Status", CurrentLine, CurrentColumn, ValidCursor ? CurrentChar.ToString() : "INVALID");
        }
    }
}