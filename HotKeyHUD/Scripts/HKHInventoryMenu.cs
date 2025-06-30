using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HKHInventoryMenu : DaggerfallInventoryWindow
    {
        private int lastSelectedSlot = -1;
        private readonly HKHMenuPopup hotKeyMenuPopup;
        public HKHUtil.KeyItemHandler OnKeyItem;
        public EventHandler<List<DaggerfallUnityItem>> OnInventoryClose;
        public HKHUtil.SenderHandler OnOpen;

        public HKHInventoryMenu(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null) : base(uiManager, previous)
        {
            hotKeyMenuPopup = HKHMenuPopup.Instance;
        }

        public override void OnPush()
        {
            base.OnPush();
            RaiseOnInventoryOpenEvent(this);
        }

        public override void OnPop()
        {
            base.OnPop();
            RaiseOnInventoryClose(remoteItemListScroller.Items);
        }

        protected override void Setup()
        {
            base.Setup();
            NativePanel.Components.Add(hotKeyMenuPopup);
        }

        protected override void LocalItemListScroller_OnItemClick(DaggerfallUnityItem item, ActionModes actionMode)
        {
            if (hotKeyMenuPopup.Enabled && item.currentCondition > 0 && !HKHUtil.GetProhibited(item))
            {
                RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(item, hotKeyMenuPopup.SelectedSlot, this, hotKeyMenuPopup));
                return;
            }

            base.LocalItemListScroller_OnItemClick(item, actionMode);
        }

        protected override void PaperDoll_OnMouseClick(BaseScreenComponent sender, Vector2 position, ActionModes actionMode)
        {
            var equipInd = paperDoll.GetEquipIndex((int)position.x, (int)position.y);
            if (equipInd == 0xFF) // No item
                return;
            var slot = (EquipSlots)equipInd;
            var item = playerEntity.ItemEquipTable.GetItem(slot);
            if (item != null && hotKeyMenuPopup.Enabled && item.currentCondition > 0 && !HKHUtil.GetProhibited(item))
            {
                RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(item, hotKeyMenuPopup.SelectedSlot, this, hotKeyMenuPopup));
                return;
            }

            base.PaperDoll_OnMouseClick(sender, position, actionMode);
        }

        protected override void AccessoryItemsButton_OnLeftMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            var slot = (EquipSlots)sender.Tag;
            var item = playerEntity.ItemEquipTable.GetItem(slot);
            if (item != null && hotKeyMenuPopup.Enabled && item.currentCondition > 0 && !HKHUtil.GetProhibited(item))
            {
                RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(item, hotKeyMenuPopup.SelectedSlot, this, hotKeyMenuPopup));
                return;
            }

            base.AccessoryItemsButton_OnLeftMouseClick(sender, position);
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

        private void RaiseKeyItemEvent(HKHUtil.KeyItemEventArgs args)
        {
            OnKeyItem?.Invoke(args);
        }

        private void RaiseOnInventoryClose(List<DaggerfallUnityItem> items)
        {
            OnInventoryClose?.Invoke(this, items);
        }

        private void RaiseOnInventoryOpenEvent(object sender)
        {
            OnOpen?.Invoke(this);
        }
    }
}
