#if DNXCORE50
namespace System.Collections.Generic
{
    internal static class ListExtensions
    {
        public static IList<T> AsReadOnly<T>(this IList<T> list)
        {
            return list;
        }
    }
}
#endif
