// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Dnx.Compilation.CSharp;

namespace Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages
{
    public class CompilationOptionsMessage
    {
        public FrameworkData Framework { get; set; }

        public CompilationSettings CompilationOptions { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as CompilationOptionsMessage;

            return other != null &&
                 object.Equals(Framework, other.Framework) &&
                 object.Equals(CompilationOptions, other.CompilationOptions);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}