using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public sealed class DefaultFrameworkNameProvider : FrameworkNameProvider
    {
        public DefaultFrameworkNameProvider()
            : base(new IFrameworkMappings[] { DefaultFrameworkMappings.Instance },
                new IPortableFrameworkMappings[] { DefaultPortableFrameworkMappings.Instance })
        {

        }

        private static IFrameworkNameProvider _instance;
        public static IFrameworkNameProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DefaultFrameworkNameProvider();
                }

                return _instance;
            }
        }
    }
}
