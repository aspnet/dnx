namespace RuntimeRestoreTest
{
    public static class Helper
    {
        public static string GetValue()
        {
            // This one does double-duty as the Windows 7 and Windows 8 one (to test fallback)
            return "This is the Windows 7/8 x64 version.";
        }
    }
}
