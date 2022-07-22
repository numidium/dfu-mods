using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotkeyHUDInventoryMenu : DaggerfallInventoryWindow
    {
        int slotNum;
        int lastSelectedSlot = -1;
        DaggerfallUnityItem hotKeyItem;
        readonly HotKeyDisplay hotKeyDisplay;
        readonly HotKeyMenuPopup hotKeyMenuPopup;

        public HotkeyHUDInventoryMenu(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null) : base(uiManager, previous)
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
            hotKeyMenuPopup.HandleSlotSelect(ref lastSelectedSlot);
        }

        protected override void LocalItemListScroller_OnItemClick(DaggerfallUnityItem item, ActionModes actionMode)
        {
            if (hotKeyMenuPopup.Enabled && HotKeyHUD.KeyItem(item, ref slotNum, uiManager, this, hotKeyMenuPopup, ActionSelectDialog_OnButtonClick, ref hotKeyItem, hotKeyDisplay))
                return;
            base.LocalItemListScroller_OnItemClick(item, actionMode);
        }

        protected override void PaperDoll_OnMouseClick(BaseScreenComponent sender, Vector2 position, ActionModes actionMode)
        {
            var equipInd = paperDoll.GetEquipIndex((int)position.x, (int)position.y);
            if (equipInd == 0xff) // No item
                return;
            var slot = (EquipSlots)equipInd;
            var item = playerEntity.ItemEquipTable.GetItem(slot);
            if (hotKeyMenuPopup.Enabled && (item == null || HotKeyHUD.KeyItem(item, ref slotNum, uiManager, this, hotKeyMenuPopup, ActionSelectDialog_OnButtonClick, ref hotKeyItem, hotKeyDisplay)))
                return;
            base.PaperDoll_OnMouseClick(sender, position, actionMode);
        }

        protected override void AccessoryItemsButton_OnLeftMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            var slot = (EquipSlots)sender.Tag;
            var item = playerEntity.ItemEquipTable.GetItem(slot);
            if (hotKeyMenuPopup.Enabled && (item == null || HotKeyHUD.KeyItem(item, ref slotNum, uiManager, this, hotKeyMenuPopup, ActionSelectDialog_OnButtonClick, ref hotKeyItem, hotKeyDisplay)))
                return;
            base.AccessoryItemsButton_OnLeftMouseClick(sender, position);
        }

        private void ActionSelectDialog_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            sender.CloseWindow();
            var forceUse = false;
            if (sender.SelectedButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
                forceUse = true;
            hotKeyDisplay.SetItemAtSlot(hotKeyItem, slotNum, forceUse);
            hotKeyMenuPopup.SyncIcons();
        }
    }
}
