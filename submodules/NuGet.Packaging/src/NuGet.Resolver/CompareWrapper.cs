using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    /// <summary>
    /// Simple helper class to provide an IComparer instance based on a comparison function
    /// </summary>
    /// <typeparam name="T">The type to compare.</typeparam>
    public class CompareWrapper<T> : IComparer<T>
    {
        private readonly Func<T, T, int> compareImpl;

        public CompareWrapper(Func<T, T, int> compareImpl)
        {
            if (compareImpl == null)
            {
                throw new ArgumentNullException("compareImpl");
            }
            this.compareImpl = compareImpl;
        }

        public int Compare(T x, T y)
        {
            return compareImpl(x, y);
        }
    }
}
