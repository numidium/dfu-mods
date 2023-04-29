using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HotkeyHUDInventoryMenu : DaggerfallInventoryWindow
    {
        private int slotNum;
        private int lastSelectedSlot = -1;
        private DaggerfallUnityItem hotKeyItem;
        private readonly HotKeyMenuPopup hotKeyMenuPopup;

        public HotkeyHUDInventoryMenu(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null) : base(uiManager, previous)
        {
            hotKeyMenuPopup = HotKeyMenuPopup.Instance;
        }

        public override void Update()
        {
            base.Update();
            hotKeyMenuPopup.HandleSlotSelect(ref lastSelectedSlot);
        }

        public override void OnPop()
        {
            // Remove discarded items from keyed buttons.
            var hotKeyDisplay = HotKeyDisplay.Instance;
            var hotKeyButtons = hotKeyDisplay.HotKeyButtons;
            for (var i = 0; i < hotKeyButtons.Length; i++)
            {
                if (hotKeyButtons[i].Payload is DaggerfallUnityItem item && remoteItemListScroller.Items.Contains(item))
                    hotKeyDisplay.SetItemAtSlot(null, i);
            }

            base.OnPop();
        }

        protected override void Setup()
        {
            base.Setup();
            NativePanel.Components.Add(hotKeyMenuPopup);
        }

        protected override void LocalItemListScroller_OnItemClick(DaggerfallUnityItem item, ActionModes actionMode)
        {
            if (hotKeyMenuPopup.Enabled && HotKeyDisplay.Instance.KeyItem(item, ref slotNum, uiManager, this, hotKeyMenuPopup, ActionSelectDialog_OnButtonClick, ref hotKeyItem))
                return;
            base.LocalItemListScroller_OnItemClick(item, actionMode);
        }

        protected override void PaperDoll_OnMouseClick(BaseScreenComponent sender, Vector2 position, ActionModes actionMode)
        {
            var equipInd = paperDoll.GetEquipIndex((int)position.x, (int)position.y);
            if (equipInd == 0xFF) // No item
                return;
            var slot = (EquipSlots)equipInd;
            var item = playerEntity.ItemEquipTable.GetItem(slot);
            if (hotKeyMenuPopup.Enabled && (item == null || HotKeyDisplay.Instance.KeyItem(item, ref slotNum, uiManager, this, hotKeyMenuPopup, ActionSelectDialog_OnButtonClick, ref hotKeyItem)))
                return;
            base.PaperDoll_OnMouseClick(sender, position, actionMode);
        }

        protected override void AccessoryItemsButton_OnLeftMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            var slot = (EquipSlots)sender.Tag;
            var item = playerEntity.ItemEquipTable.GetItem(slot);
            if (hotKeyMenuPopup.Enabled && (item == null || HotKeyDisplay.Instance.KeyItem(item, ref slotNum, uiManager, this, hotKeyMenuPopup, ActionSelectDialog_OnButtonClick, ref hotKeyItem)))
                return;
            base.AccessoryItemsButton_OnLeftMouseClick(sender, position);
        }

        private void ActionSelectDialog_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            sender.CloseWindow();
            var forceUse = false;
            if (sender.SelectedButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
                forceUse = true;
            HotKeyDisplay.Instance.SetItemAtSlot(hotKeyItem, slotNum, forceUse);
        }
    }
}
