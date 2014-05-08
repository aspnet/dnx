using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Common
{
    internal class ExceptionHelper
    {
        public static IEnumerable<string> GetExceptions(Exception ex)
        {
            if (ex.InnerException != null)
            {
                foreach (var e in GetExceptions(ex.InnerException))
                {
                    yield return e;
                }
            }

            if (!(ex is TargetInvocationException))
            {
                yield return ex.ToString();
            }
        }
    }
}
