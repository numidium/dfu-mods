using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyDisplay : Panel
    {
        const float iconsY = 177f;
        bool initialized = false;
        HotKeyButton[] hotKeyButtons;
        DaggerfallUnityItem lastRightHandItem;
        DaggerfallUnityItem lastLeftHandItem;
        public List<HotKeyButton> ButtonList => hotKeyButtons.ToList();

        public HotKeyDisplay() : base()
        {
            Enabled = false;
        }

        public override void Update()
        {
            if (!Enabled)
                return;
            if (!initialized)
                Initialize();

            base.Update();
            var hud = DaggerfallUI.Instance.DaggerfallHUD;
            if (Scale != hud.HUDCompass.Scale)
                SetScale(hud.HUDCompass.Scale); // Compass is an arbitrary choice to get scale. Doesn't matter which HUD element is used.
            var keyDown = InputManager.Instance.GetAnyKeyDown();
            if (keyDown >= KeyCode.Alpha1 && keyDown <= KeyCode.Alpha9)
                OnHotKeyPress(keyDown);
            foreach (var button in hotKeyButtons)
            {
                if (button.ConditionBar.Enabled && button.Payload is DaggerfallUnityItem item)
                    button.UpdateCondition(item.ConditionPercentage, Scale);
            }
        }

        public override void Draw()
        {
            if (!Enabled)
                return;
            base.Draw();
        }

        public void SetItemAtSlot(DaggerfallUnityItem item, int index, bool forceUse = false)
        {
            for (var i = 0; i < hotKeyButtons.Length; i++)
                if (RemoveDuplicateIfAt(index, i, hotKeyButtons[i].Payload == item))
                    break;

            hotKeyButtons[index].SetItem(item, forceUse);
        }

        public DaggerfallUnityItem GetItemAtSlot(int index)
        {
            if (hotKeyButtons[index].Payload is DaggerfallUnityItem item)
                return item;
            return null;
        }

        public void SetSpellAtSlot(in EffectBundleSettings spell, int index)
        {
            for (var i = 0; i < hotKeyButtons.Length; i++)
                if (RemoveDuplicateIfAt(index, i, hotKeyButtons[i].Payload != null && hotKeyButtons[i].Payload.Equals(spell)))
                    break;

            hotKeyButtons[index].SetSpell(spell);
        }

        public void ResetButtons()
        {
            if (!initialized)
                return;
            var i = 0;
            foreach (var button in hotKeyButtons)
                SetItemAtSlot(null, i++);
        }

        private bool RemoveDuplicateIfAt(int index, int i, bool condition)
        {
            if (i != index && condition)
            {
                hotKeyButtons[i].SetItem(null);
                return true;
            }

            return false;
        }

        private void OnHotKeyPress(KeyCode keyCode)
        {
            var index = keyCode - KeyCode.Alpha1;
            var slot = hotKeyButtons[index].Payload;
            if (slot == null)
                return;
            if (slot is DaggerfallUnityItem item)
                HandleItemHotkeyPress(item, index);
            else if (slot is EffectBundleSettings spell)
                hotKeyButtons[index].HandleSpellHotkeyPress(ref spell);
        }

        private void HandleItemHotkeyPress(DaggerfallUnityItem item, int index)
        {
            hotKeyButtons[index].HandleItemHotkeyPress(item);
            // Do equip delay for weapons.
            if (item.ItemGroup == ItemGroups.Weapons)
            {
                SetEquipDelayTime();
                var weaponManager = GameManager.Instance.WeaponManager;
                // Show "equipping" message if a delay was added.
                ShowEquipDelayMessage(weaponManager.EquipCountdownRightHand, EquipSlots.RightHand);
                ShowEquipDelayMessage(weaponManager.EquipCountdownLeftHand, EquipSlots.LeftHand);
            }
        }

        private void Initialize()
        {
            // Init buttons/icons.
            Components.Clear();
            hotKeyButtons = new HotKeyButton[HotKeyHUD.iconCount];
            float xPosition = 0f;
            var itemBackdrops = HotKeyHUD.ItemBackdrops;
            for (int i = 0; i < hotKeyButtons.Length; i++)
            {
                var position = new Vector2 { x = xPosition, y = iconsY };
                hotKeyButtons[i] = new HotKeyButton(itemBackdrops[i], position, i + 1);
                xPosition += HotKeyButton.iconWidth;
                Components.Add(hotKeyButtons[i]);
            }

            // Init equip/unequip delay.
            var player = GameManager.Instance.PlayerEntity;
            // Note: Player's item table is not initialized at this point.
            // These two fields need to be set after it is inited and before player toggles slot.
            lastRightHandItem = player.ItemEquipTable.GetItem(EquipSlots.RightHand);
            lastLeftHandItem = player.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            initialized = true;
        }

        private void SetScale(Vector2 scale)
        {
            Scale = scale;
            for (int i = 0; i < hotKeyButtons.Length; i++)
                hotKeyButtons[i].SetScale(scale);
        }

        private void SetEquipDelayTime()
        {
            int delayTimeRight = 0;
            int delayTimeLeft = 0;
            var player = GameManager.Instance.PlayerEntity;
            DaggerfallUnityItem currentRightHandItem = player.ItemEquipTable.GetItem(EquipSlots.RightHand);
            DaggerfallUnityItem currentLeftHandItem = player.ItemEquipTable.GetItem(EquipSlots.LeftHand);

            if (lastRightHandItem != currentRightHandItem)
            {
                // Add delay for unequipping old item
                if (lastRightHandItem != null)
                    delayTimeRight = WeaponManager.EquipDelayTimes[lastRightHandItem.GroupIndex];

                // Add delay for equipping new item
                if (currentRightHandItem != null)
                    delayTimeRight += WeaponManager.EquipDelayTimes[currentRightHandItem.GroupIndex];
            }
            if (lastLeftHandItem != currentLeftHandItem)
            {
                // Add delay for unequipping old item
                if (lastLeftHandItem != null)
                    delayTimeLeft = WeaponManager.EquipDelayTimes[lastLeftHandItem.GroupIndex];

                // Add delay for equipping new item
                if (currentLeftHandItem != null)
                    delayTimeLeft += WeaponManager.EquipDelayTimes[currentLeftHandItem.GroupIndex];
            }

            lastRightHandItem = currentRightHandItem;
            lastLeftHandItem = currentLeftHandItem;

            GameManager.Instance.WeaponManager.EquipCountdownRightHand += delayTimeRight;
            GameManager.Instance.WeaponManager.EquipCountdownLeftHand += delayTimeLeft;
        }

        private static void ShowEquipDelayMessage(float countDownValue, EquipSlots equipSlot)
        {
            if (countDownValue > 0)
            {
                var currentWeapon = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(equipSlot);
                if (currentWeapon != null)
                {
                    var message = TextManager.Instance.GetLocalizedText("equippingWeapon");
                    message = message.Replace("%s", currentWeapon.ItemTemplate.name);
                    DaggerfallUI.Instance.PopupMessage(message);
                }
            }
        }
    }
}
