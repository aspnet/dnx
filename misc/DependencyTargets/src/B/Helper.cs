namespace B
{
    public static class Helper
    {
        public static string GetValue()
        {
#if PACKAGE
            return "This is Package B";
#else
            return "This is Project B";
#endif
        }
    }
}
