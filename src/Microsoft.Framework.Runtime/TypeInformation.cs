using System;

namespace Microsoft.Framework.Runtime
{
    public class TypeInformation
    {
        private readonly Tuple<string, string> _tuple;

        public TypeInformation(string assemblyName, string typeName)
        {
            _tuple = Tuple.Create(assemblyName, typeName);
        }

        public string AssemblyName
        {
            get
            {
                return _tuple.Item1;
            }
        }

        public string TypeName
        {
            get
            {
                return _tuple.Item2;
            }
        }

        public override int GetHashCode()
        {
            return _tuple.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var li = obj as TypeInformation;

            return li != null && li._tuple.Equals(_tuple);
        }
    }
}