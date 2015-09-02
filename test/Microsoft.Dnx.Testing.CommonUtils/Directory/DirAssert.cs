namespace Microsoft.Dnx.Testing
{
    public class DirAssert
    {
        public static void Equal(Dir expected, Dir actual)
        {
            var diff = actual.Diff(expected);
            if (diff.IsEmpty)
            {
                return;
            }
            throw new DirMismatchException(expected.LoadPath, actual.LoadPath, diff);
        }
    }
}
