using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class ApplicationEnvironmentFacts
    {
        [Fact]
        public void GetDataReturnsNullForNonExistantKey()
        {
            var appEnv = new ApplicationEnvironment(null, null, null, null);
            Assert.Null(appEnv.GetData("not-a-real-key"));
        }

        [Fact]
        public void GetSetDataPersistDataWithinASingleApplicationEnvironmentInstance()
        {
            var appEnv = new ApplicationEnvironment(null, null, null, null);
            var obj = new object();
            var key = "testGetSet:" + Guid.NewGuid().ToString("N");
            appEnv.SetData(key, obj);
            Assert.Same(obj, appEnv.GetData(key));
        }

#if DNX451
        [Fact]
        public void GetSetDataPersistDataGloballyAcrossAppDomainInDesktopClr()
        {
            var appEnv1 = new ApplicationEnvironment(null, null, null, null);
            var appEnv2 = new ApplicationEnvironment(null, null, null, null);
            var obj = new object();
            var key = "testGetSetGlobal:" + Guid.NewGuid().ToString("N");
            appEnv1.SetData(key, obj);
            Assert.Same(obj, appEnv2.GetData(key));
            Assert.Same(obj, AppDomain.CurrentDomain.GetData(key));
        }
#else
        [Fact]
        public void GetSetDataPersistDataWithinEachApplicationEnvironment()
        {
            var appEnv1 = new ApplicationEnvironment(null, null, null, null);
            var appEnv2 = new ApplicationEnvironment(null, null, null, null);
            var key = "testGetSetDifferent:" + Guid.NewGuid().ToString("N");
            appEnv1.SetData(key, new object());
            appEnv2.SetData(key, new object());
            Assert.NotSame(appEnv1.GetData(key), appEnv2.GetData(key));
        }

        // This test isn't needed in DNX451 because we use AppDomain.CurrentDomain to store data
        [Fact]
        public void GetSetDataSharesWithHostEnvironmentIfProvided()
        {
            var appEnv1 = new ApplicationEnvironment(null, null, null, null);
            var appEnv2 = new ApplicationEnvironment(null, null, null, appEnv1);
            var key1 = "testGetSetShared:" + Guid.NewGuid().ToString("N");
            var key2 = "testGetSetShared2:" + Guid.NewGuid().ToString("N");
            var obj1 = new object();
            var obj2 = new object();
            appEnv1.SetData(key1, obj1);
            appEnv2.SetData(key2, obj2);
            Assert.Same(obj1, appEnv2.GetData(key1));
            Assert.Same(obj2, appEnv1.GetData(key2));
        }
#endif
    }
}
