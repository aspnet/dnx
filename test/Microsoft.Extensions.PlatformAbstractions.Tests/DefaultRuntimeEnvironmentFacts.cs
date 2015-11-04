using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Extensions.PlatformAbstractions.Tests
{
	public class DefaultRuntimeEnvironmentFacts
	{
		[Fact]
		public void CanDetectOSNameAndVersionCorrectly()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				CanDetectOSXNameAndVersionCorrectly();
			}
			else
			{
				Assert.False(true, "Operating system not yet implemented");
			}
		}
		
		private void CanDetectOSXNameAndVersionCorrectly()
		{
			var runtimeEnvironment = new DefaultRuntimeEnvironment();
			Assert.Equal("Darwin", runtimeEnvironment.OperatingSystem);
			Assert.True(runtimeEnvironment.OperatingSystemVersion.StartsWith("10."));
			
			// Can't assert the specific version because we don't know what version they are on!
		}
	}
}