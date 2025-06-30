using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HKHSpellbookWindow : DaggerfallSpellBookWindow
    {
        private int lastSelectedSlot = -1;
        private readonly HKHMenuPopup hotKeyMenuPopup;
        public event EventHandler<HKHUtil.KeyItemEventArgs> OnKeyItem;
        public HKHUtil.SenderHandler OnOpen;
        public HKHUtil.SenderHandler OnSpellbookClose;

        public HKHSpellbookWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null, bool buyMode = false) : base(uiManager, previous, buyMode)
        {
            hotKeyMenuPopup = HKHMenuPopup.Instance;
        }

        protected override void Setup()
        {
            base.Setup();
            NativePanel.Components.Add(hotKeyMenuPopup);
        }

        public override void OnPush()
        {
            base.OnPush();
            RaiseOnOpenEvent();
        }

        public override void OnPop()
        {
            base.OnPop();
            RaiseSpellbookCloseEvent();
        }

        protected override void SpellsListBox_OnSelectItem()
        {
            base.SpellsListBox_OnSelectItem();
            if (hotKeyMenuPopup.Enabled)
            {
                var spellBook = GameManager.Instance.PlayerEntity.GetSpells();
                var spell = spellBook[spellsListBox.SelectedIndex];
                RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(spell, hotKeyMenuPopup.SelectedSlot, this, hotKeyMenuPopup));
                DaggerfallUI.Instance.PlayOneShot(DaggerfallWorkshop.SoundClips.ButtonClick);
            }
        }

        private void RaiseKeyItemEvent(HKHUtil.KeyItemEventArgs args)
        {
            OnKeyItem?.Invoke(this, args);
        }

        private void RaiseOnOpenEvent()
        {
            OnOpen?.Invoke(this);
        }

        private void RaiseSpellbookCloseEvent()
        {
            OnSpellbookClose?.Invoke(this);
        }

        public void HandleKeyDown(KeyCode keyCode)
        {
            if (keyCode > KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
                hotKeyMenuPopup.SelectSlot(keyCode - KeyCode.Alpha1, ref lastSelectedSlot);
        }

        public void HandleKeyUp(KeyCode keyCode)
        {
            if (keyCode > KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9 && keyCode - KeyCode.Alpha1 == lastSelectedSlot)
                hotKeyMenuPopup.UnselectSlot();
        }
    }
}
