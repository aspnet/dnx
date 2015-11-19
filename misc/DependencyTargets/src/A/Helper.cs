namespace A
{
    public static class Helper
    {
        public static string GetValue()
        {
#if PACKAGE
            return "This is Package A";
#else
            return "This is Project A";
#endif
        }
    }
}
