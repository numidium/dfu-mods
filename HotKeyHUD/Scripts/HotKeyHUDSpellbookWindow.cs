using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace HotKeyHUD
{
    public sealed class HotKeyHUDSpellbookWindow : DaggerfallSpellBookWindow
    {
        private int lastSelectedSlot = -1;
        private readonly HotKeyMenuPopup hotKeyMenuPopup;

        public HotKeyHUDSpellbookWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null, bool buyMode = false) : base(uiManager, previous, buyMode)
        {
            hotKeyMenuPopup = HotKeyMenuPopup.Instance;
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
        }

        protected override void SpellsListBox_OnSelectItem()
        {
            base.SpellsListBox_OnSelectItem();
            if (hotKeyMenuPopup.Enabled)
            {
                var spellBook = GameManager.Instance.PlayerEntity.GetSpells();
                var spell = spellBook[spellsListBox.SelectedIndex];
                HotKeyDisplay.Instance.SetSpellAtSlot(in spell, hotKeyMenuPopup.SelectedSlot);
                DaggerfallUI.Instance.PlayOneShot(DaggerfallWorkshop.SoundClips.ButtonClick);
            }
        }
    }
}
