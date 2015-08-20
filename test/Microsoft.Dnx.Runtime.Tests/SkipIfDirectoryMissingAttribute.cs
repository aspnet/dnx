using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Testing.xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SkipIfReferenceAssembliesMissingAttribute : Attribute, ITestCondition
    {
        private readonly string _directory;

        /// <summary>
        /// Marks the attached test as skipped if the specified directory does not exist on the test machine
        /// </summary>
        /// <param name="directory">The directory that must be present for the test to run. Supports environment variables using %VARIABLENAME% syntax</param>
        public SkipIfReferenceAssembliesMissingAttribute(string directory)
        {
            _directory = ExpandReferenceAssemblyPath(directory);
        }

        public bool IsMet
        {
            get
            {
                return Directory.Exists(_directory);
            }
        }

        public string SkipReason
        {
            get
            {
                return $"The required directory '{_directory}' is not present on the test machine";
            }
        }

        public static string ExpandReferenceAssemblyPath(string path)
        {
            return Environment.ExpandEnvironmentVariables(
                path.Replace(
                    "%REFASMSROOT%", 
                    FrameworkReferenceResolver.GetReferenceAssembliesPath()));
        }
    }
}
