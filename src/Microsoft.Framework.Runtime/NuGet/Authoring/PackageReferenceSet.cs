// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet
{
    public class PackageReferenceSet : IFrameworkTargetable
    {
        private readonly FrameworkName _targetFramework;
        private readonly ICollection<string> _references;

        public PackageReferenceSet(IEnumerable<string> references)
            : this(null, references)
        {
        }

        public PackageReferenceSet(FrameworkName targetFramework, IEnumerable<string> references)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            _targetFramework = targetFramework;
            _references = new ReadOnlyCollection<string>(references.ToList());
        }

        public ICollection<string> References
        {
            get { return _references; }
        }

        public FrameworkName TargetFramework
        {
            get { return _targetFramework; }
        }

        public IEnumerable<FrameworkName> SupportedFrameworks
        {
            get
            {
                if (TargetFramework == null)
                {
                    yield break;
                }

                yield return TargetFramework;
            }
        }
    }
}
