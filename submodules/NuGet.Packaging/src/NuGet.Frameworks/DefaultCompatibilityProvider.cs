using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public sealed class DefaultCompatibilityProvider : CompatibilityProvider
    {

        public DefaultCompatibilityProvider()
            : base(DefaultFrameworkNameProvider.Instance)
        {

        }

        private static IFrameworkCompatibilityProvider _instance;
        public static IFrameworkCompatibilityProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DefaultCompatibilityProvider();
                }

                return _instance;
            }
        }

    }
}
