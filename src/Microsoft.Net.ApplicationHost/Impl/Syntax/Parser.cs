namespace Microsoft.Net.ApplicationHost.Impl.Syntax
{
    internal delegate Result<TValue> Parser<TValue>(Cursor cursor);
}