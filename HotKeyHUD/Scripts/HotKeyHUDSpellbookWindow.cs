using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyHUDSpellbookWindow : DaggerfallSpellBookWindow
    {
        int lastSelectedSlot = -1;
        readonly HotKeyDisplay hotKeyDisplay;
        readonly HotKeyMenuPopup hotKeyMenuPopup;

        public HotKeyHUDSpellbookWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null, bool buyMode = false) : base(uiManager, previous, buyMode)
        {
            hotKeyDisplay = (HotKeyDisplay)DaggerfallUI.Instance.DaggerfallHUD.ParentPanel.Components.FirstOrDefault(x => x.GetType() == typeof(HotKeyDisplay));
            hotKeyMenuPopup = new HotKeyMenuPopup();
        }

        protected override void Setup()
        {
            base.Setup();
            NativePanel.Components.Add(hotKeyMenuPopup);
        }

        public override void OnPush()
        {
            base.OnPush();
            if (hotKeyMenuPopup.Initialized)
                hotKeyMenuPopup.SyncIcons();
        }

        public override void Update()
        {
            base.Update();
            // Show hotkey popup when hotkey is pressed and hide when released.
            var hotKey = KeyCode.Alpha1 - 1;
            var input = InputManager.Instance;
            for (var i = 0; i <= (int)KeyCode.Alpha9; i++)
            {
                var key = KeyCode.Alpha1 + i;
                if (input.GetKey(key))
                    hotKey = key;
            }

            if (hotKey >= KeyCode.Alpha1 && hotKey <= KeyCode.Alpha9)
            {
                hotKeyMenuPopup.Enabled = true;
                var slotNum = hotKey - KeyCode.Alpha1;
                if (slotNum != lastSelectedSlot)
                    hotKeyMenuPopup.SetSelectedSlot(slotNum);
                lastSelectedSlot = slotNum;
            }
            else
                hotKeyMenuPopup.Enabled = false;
        }

        protected override void SpellsListBox_OnSelectItem()
        {
            base.SpellsListBox_OnSelectItem();
            var hotKeyDown = KeyCode.Alpha1 - 1;
            var input = InputManager.Instance;
            for (var i = 0; i <= (int)KeyCode.Alpha9; i++)
            {
                var key = KeyCode.Alpha1 + i;
                if (input.GetKeyDown(input.GetComboCode(key, KeyCode.Mouse0)))
                    hotKeyDown = key;
            }

            if (hotKeyDown >= KeyCode.Alpha1 && hotKeyDown <= KeyCode.Alpha9)
            {
                var spellBook = GameManager.Instance.PlayerEntity.GetSpells();
                var spell = spellBook[spellsListBox.SelectedIndex];
                var slotNum = hotKeyDown - KeyCode.Alpha1;
                hotKeyDisplay.SetSpellAtSlot(in spell, slotNum);
                hotKeyMenuPopup.SyncIcons();
                DaggerfallUI.Instance.PlayOneShot(DaggerfallWorkshop.SoundClips.ButtonClick);
            }
        }
    }
}
