namespace HotKeyHUD
{
    // I believe, generally speaking, that passing a <= 16 byte struct is more performant than using a class.
    // Correct me if I'm wrong.
    public struct ItemUseEventArgs
    {
        public int Index { get; private set; }
        public object Item { get; private set; }
        public ItemUseEventArgs(int index, object item)
        {
            Index = index;
            Item = item;
        }
    }

    public struct ItemSetEventArgs
    {
        public int Index { get; private set; }
        public object Item { get; private set; }
        public bool ForceUse { get; private set; }
        public ItemSetEventArgs(int index, object item, bool forceUse)
        {
            Index = index;
            Item = item;
            ForceUse = forceUse;
        }
    }
}
