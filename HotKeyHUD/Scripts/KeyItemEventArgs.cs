using DaggerfallWorkshop.Game.UserInterface;
using System;

namespace HotKeyHUD
{
    public sealed class KeyItemEventArgs : EventArgs
    {
        public object Item { get; private set; }
        public int Slot { get; private set; }
        public IUserInterfaceWindow PreviousWindow { get; private set; }
        public HotKeyMenuPopup Popup { get; private set; }

        public KeyItemEventArgs(object item, int slot, IUserInterfaceWindow previousWindow, HotKeyMenuPopup popup)
        {
            Item = item;
            Slot = slot;
            PreviousWindow = previousWindow;
            Popup = popup;
        }
    }
}
