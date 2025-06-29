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
        public EventHandler<HKHUtil.KeyItemEventArgs> OnKeyItem;
        public EventHandler<List<DaggerfallUnityItem>> OnInventoryClose;

        public HKHInventoryMenu(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null) : base(uiManager, previous)
        {
            hotKeyMenuPopup = HKHMenuPopup.Instance;
        }

        public override void Update()
        {
            base.Update();
            hotKeyMenuPopup.HandleSlotSelect(ref lastSelectedSlot);
        }

        public override void OnPop()
        {
            RaiseOnInventoryClose(remoteItemListScroller.Items);
            base.OnPop();
        }

        protected override void Setup()
        {
            base.Setup();
            NativePanel.Components.Add(hotKeyMenuPopup);
            // This is a circular dependency and I hate it. It's not easy to get a reference to this object from the other class so this is actually the most elegant way to
            // do it unfortunately.
            OnKeyItem += HotKeyHUD.Instance.DisgustingProxyForHandleKeyItem;
            OnInventoryClose += HotKeyHUD.Instance.DisgustingProxyForHandleInventoryClose;
        }

        protected override void LocalItemListScroller_OnItemClick(DaggerfallUnityItem item, ActionModes actionMode)
        {
            if (hotKeyMenuPopup.Enabled && item.currentCondition > 0 && !HKHUtil.GetProhibited(item))
            {
                RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(item, hotKeyMenuPopup.SelectedSlot, PreviousWindow, hotKeyMenuPopup));
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
                RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(item, hotKeyMenuPopup.SelectedSlot, PreviousWindow, hotKeyMenuPopup));
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
                RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(item, hotKeyMenuPopup.SelectedSlot, PreviousWindow, hotKeyMenuPopup));
                return;
            }

            base.AccessoryItemsButton_OnLeftMouseClick(sender, position);
        }

        public void HandleKeyDown(KeyCode keyCode)
        {

        }

        public void HandleKeyUp(KeyCode keyCode)
        {

        }

        private void RaiseKeyItemEvent(HKHUtil.KeyItemEventArgs args)
        {
            OnKeyItem?.Invoke(this, args);
        }

        private void RaiseOnInventoryClose(List<DaggerfallUnityItem> items)
        {
            OnInventoryClose?.Invoke(this, items);
        }
    }
}
