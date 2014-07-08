using System;

namespace Microsoft.Framework.Project
{
    /// <summary>
    /// Crossgen can either pass, fail or with warning
    /// </summary>
    public class CrossgenResult
    {
        public bool Failed { get; private set; }
        public bool HasWarning { get; private set; }

	    public CrossgenResult()
	    {
            Failed = false;
            HasWarning = false;
	    }

        public void Fail()
        {
            Failed = true;
        }

        public void Warn()
        {
            HasWarning = true;
        }
    }
}