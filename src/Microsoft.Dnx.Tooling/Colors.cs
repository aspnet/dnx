
namespace Microsoft.Dnx.Tooling
{
    public static class Colors
    {
        public static string Yellow(this string text)
        {
            return "\x1B[33m" + text + "\x1B[39m";
        }

        public static string White(this string text)
        {
            return "\x1B[37m" + text + "\x1B[39m";
        }

        public static string Green(this string text)
        {
            return "\x1B[32m" + text + "\x1B[39m";
        }

        public static string Red(this string text)
        {
            return "\x1B[31m" + text + "\x1B[39m";
        }

        public static string Bold(this string text)
        {
            return "\x1B[1m" + text + "\x1B[22m";
        }
    }
}