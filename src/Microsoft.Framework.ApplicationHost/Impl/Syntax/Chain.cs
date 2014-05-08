namespace Microsoft.Framework.ApplicationHost.Impl.Syntax
{
    internal struct Chain<TLeft, TDown>
    {
        public Chain(TLeft left, TDown down)
            : this()
        {
            Left = left;
            Down = down;
        }

        public readonly TLeft Left;
        public readonly TDown Down;
    }
}