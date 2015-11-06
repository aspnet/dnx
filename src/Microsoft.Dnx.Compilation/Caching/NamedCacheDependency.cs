using Microsoft.Extensions.CompilationAbstractions.Caching;

namespace Microsoft.Dnx.Compilation.Caching
{
    public class NamedCacheDependency : ICacheDependency
    {
        private readonly string _name;
        private bool _hasChanged;

        public NamedCacheDependency(string name)
        {
            _name = name;
        }

        public void SetChanged()
        {
            _hasChanged = true;
        }

        public bool HasChanged
        {
            get
            {
                return _hasChanged;
            }
        }
    }
}