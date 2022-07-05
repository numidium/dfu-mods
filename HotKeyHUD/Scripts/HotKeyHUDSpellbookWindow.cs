using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyHUDSpellbookWindow : DaggerfallSpellBookWindow
    {
        HotKeyDisplay hotKeyDisplay;

        public HotKeyHUDSpellbookWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null, bool buyMode = false) : base(uiManager, previous, buyMode)
        {
            hotKeyDisplay = (HotKeyDisplay)DaggerfallUI.Instance.DaggerfallHUD.ParentPanel.Components.FirstOrDefault(x => x.GetType() == typeof(HotKeyDisplay));
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
            }
        }
    }
}
