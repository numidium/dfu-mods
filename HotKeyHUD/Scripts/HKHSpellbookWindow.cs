using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;

namespace HotKeyHUD
{
    public sealed class HKHSpellbookWindow : DaggerfallSpellBookWindow
    {
        private int lastSelectedSlot = -1;
        private readonly HKHMenuPopup hotKeyMenuPopup;
        public event EventHandler<HKHUtil.KeyItemEventArgs> OnKeyItem;

        public HKHSpellbookWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null, bool buyMode = false) : base(uiManager, previous, buyMode)
        {
            hotKeyMenuPopup = HKHMenuPopup.Instance;
        }

        public override void Update()
        {
            base.Update();
            hotKeyMenuPopup.HandleSlotSelect(ref lastSelectedSlot);
        }

        protected override void Setup()
        {
            base.Setup();
            NativePanel.Components.Add(hotKeyMenuPopup);
            // This is a circular dependency and I hate it. My hand was forced because there's no way to retrieve a reference to this from DaggerfallUI.
            OnKeyItem += HotKeyHUD.Instance.DisgustingProxyForHandleKeyItem;
        }

        protected override void SpellsListBox_OnSelectItem()
        {
            base.SpellsListBox_OnSelectItem();
            if (hotKeyMenuPopup.Enabled)
            {
                var spellBook = GameManager.Instance.PlayerEntity.GetSpells();
                var spell = spellBook[spellsListBox.SelectedIndex];
                RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(spell, hotKeyMenuPopup.SelectedSlot, PreviousWindow, hotKeyMenuPopup));
                DaggerfallUI.Instance.PlayOneShot(DaggerfallWorkshop.SoundClips.ButtonClick);
            }
        }

        private void RaiseKeyItemEvent(HKHUtil.KeyItemEventArgs args)
        {
            OnKeyItem?.Invoke(this, args);
        }
    }
}
