using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HotKeyDisplay : Panel
    {
        private bool initialized = false;
        private readonly PlayerEntity playerEntity;
        private readonly HotKeyMenuPopup hotKeyMenuPopup;
        private static HotKeyDisplay instance;
        public HotKeyButton[] HotKeyButtons { get; private set; }
        public HotKeyButton EquippedButton { get; private set; }
        public HotKeyUtil.HUDVisibility Visibility { private get; set; }
        public bool EquipDelayDisabled { private get; set; }
        public static HotKeyDisplay Instance
        {
            get
            {
                if (instance == null)
                    instance = new HotKeyDisplay();
                return instance;
            }
        }
        
        private HotKeyDisplay() : base()
        {
            Enabled = false;
            playerEntity = GameManager.Instance.PlayerEntity;
            hotKeyMenuPopup = HotKeyMenuPopup.Instance;
            AutoSize = AutoSizeModes.ResizeToFill;
            Size = DaggerfallUI.Instance.DaggerfallHUD.NativePanel.Size;
        }

        public Func<string, string> Localize { get; set; }

        public override void Update()
        {
            if (!Enabled)
                return;
            if (!initialized)
                Initialize();

            base.Update();
            var hud = DaggerfallUI.Instance.DaggerfallHUD;
            if (Scale != hud.NativePanel.LocalScale)
                SetScale(hud.NativePanel.LocalScale);
            var keyDown = InputManager.Instance.GetAnyKeyDown();
            if (keyDown >= KeyCode.Alpha1 && keyDown <= KeyCode.Alpha9)
                OnHotKeyPress(keyDown);
            // Item polling/updating
            for (var i = 0; i < HotKeyButtons.Length; i++)
            {
                var button = HotKeyButtons[i];
                if (button.Payload is DaggerfallUnityItem item)
                {
                    // Remove item from hotkeys if it is:
                    // 1. no longer in inventory
                    // 2. broken from use
                    // 3. a consumed stack
                    if (button.ConditionBar.Enabled)
                    {
                        if (!playerEntity.Items.Contains(item.UID) || item.currentCondition <= 0)
                            SetButtonItem(i, null);
                        // Scaling fix. Scaling seems to break if the parent panel height > width.
                        if (button.Icon.InteriorHeight > HotKeyButton.buttonHeight * Scale.y)
                            button.Icon.Size *= .9f;
                    }
                    else if (button.StackLabel.Enabled && item.stackCount == 0)
                        SetButtonItem(i, null);
                }
            }

            var weaponManager = GameManager.Instance.WeaponManager;
            if (weaponManager.UsingRightHand)
            {
                var item = playerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
                if (item != (DaggerfallUnityItem)EquippedButton.Payload)
                    EquippedButton.SetItem(item);
            }
            else
            {
                var item = playerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
                if (item != (DaggerfallUnityItem)EquippedButton.Payload)
                    EquippedButton.SetItem(item);
            }

            // Update button visibility
            if (Visibility == HotKeyUtil.HUDVisibility.Equipped && !EquippedButton.Enabled)
            {
                EquippedButton.Enabled = true;
                foreach (var button in HotKeyButtons)
                    button.Enabled = false;
            }
            else if (Visibility == HotKeyUtil.HUDVisibility.Full && EquippedButton.Enabled)
            {
                EquippedButton.Enabled = false;
                foreach (var button in HotKeyButtons)
                    button.Enabled = true;
            }
        }

        public override void Draw()
        {
            if (!Enabled || Visibility == HotKeyUtil.HUDVisibility.None)
                return;
            base.Draw();
        }

        public void SetItemAtSlot(DaggerfallUnityItem item, int index, bool forceUse = false)
        {
            if (item != null)
                for (var i = 0; i < HotKeyButtons.Length; i++)
                    if (RemoveDuplicateIfAt(index, i, HotKeyButtons[i].Payload == item))
                        break;
            SetButtonItem(index, item, forceUse);
        }

        public DaggerfallUnityItem GetItemAtSlot(int index)
        {
            if (HotKeyButtons[index].Payload is DaggerfallUnityItem item)
                return item;
            return null;
        }

        public void SetSpellAtSlot(in EffectBundleSettings spell, int index)
        {
            for (var i = 0; i < HotKeyButtons.Length; i++)
                if (RemoveDuplicateIfAt(index, i, HotKeyButtons[i].Payload != null &&
                        HotKeyButtons[i].Payload is EffectBundleSettings settings &&
                        HotKeyUtil.CompareSpells(settings, spell)))
                    break;
            SetButtonSpell(index, spell);
        }

        public void ResetButtons()
        {
            if (!initialized)
                return;
            var i = 0;
            foreach (var button in HotKeyButtons)
                SetButtonItem(i++, null);
            EquippedButton.SetItem(null);
        }

        public void KeyItem(DaggerfallUnityItem item, ref int slotNum, IUserInterfaceManager uiManager, IUserInterfaceWindow prevWindow, HotKeyMenuPopup hotKeyMenuPopup,
                DaggerfallMessageBox.OnButtonClickHandler onButtonClickHandler, ref DaggerfallUnityItem hotKeyItem)
        {
            const string actionTypeSelectKey = "KeyAsUse";
            slotNum = hotKeyMenuPopup.SelectedSlot;
            hotKeyItem = item;
            var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
            // Show prompt if enchanted item can be either equipped or used.
            if (item != GetItemAtSlot(slotNum) && item.IsEnchanted && equipTable.GetEquipSlot(item) != EquipSlots.None && HotKeyUtil.GetEnchantedItemIsUseable(item))
            {
                var actionSelectDialog = new DaggerfallMessageBox(uiManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, Localize(actionTypeSelectKey), prevWindow);
                actionSelectDialog.OnButtonClick += onButtonClickHandler;
                actionSelectDialog.Show();
            }
            else
            {
                DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                SetItemAtSlot(hotKeyItem, slotNum);
            }
        }

        /// <summary>
        /// Set own button's item and sync with popup.
        /// </summary>
        /// <param name="index">Button index.</param>
        /// <param name="item">Item to be assigned to button.</param>
        /// <param name="forceUse">Whether or not to "Use" the item on activation.</param>
        private void SetButtonItem(int index, DaggerfallUnityItem item, bool forceUse = false)
        {
            HotKeyButtons[index].SetItem(item, forceUse);
            hotKeyMenuPopup.HotKeyButtons[index].SetItem(item, forceUse);
        }

        private void SetButtonSpell(int index, EffectBundleSettings spell)
        {
            HotKeyButtons[index].SetSpell(spell);
            hotKeyMenuPopup.HotKeyButtons[index].SetSpell(spell);
        }

        private bool RemoveDuplicateIfAt(int index, int i, bool condition)
        {
            if (i != index && condition)
            {
                SetButtonItem(i, null);
                return true;
            }

            return false;
        }

        private void OnHotKeyPress(KeyCode keyCode)
        {
            var index = keyCode - KeyCode.Alpha1;
            var slot = HotKeyButtons[index].Payload;
            if (slot == null)
                return;
            var racialOverride = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();
            var suppressInventory = racialOverride != null && racialOverride.GetSuppressInventory(out _);
            if (slot is EffectBundleSettings spell)
                HotKeyButtons[index].HandleSpellHotkeyPress(ref spell);
            else if (slot is DaggerfallUnityItem item && !suppressInventory)
                HandleItemHotkeyPress(item, index);
        }

        private void HandleItemHotkeyPress(DaggerfallUnityItem item, int index)
        {
            var lastRightHandItem = playerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            var lastLeftHandItem = playerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            HotKeyButtons[index].HandleItemHotkeyPress(item);
            // Do equip delay for weapons.
            if (!EquipDelayDisabled && item.ItemGroup == ItemGroups.Weapons || item.IsShield)
            {
                SetEquipDelayTime(lastRightHandItem, lastLeftHandItem);
                var weaponManager = GameManager.Instance.WeaponManager;
                // Show "equipping" message if a delay was added.
                ShowEquipDelayMessage(weaponManager.EquipCountdownRightHand, EquipSlots.RightHand);
                ShowEquipDelayMessage(weaponManager.EquipCountdownLeftHand, EquipSlots.LeftHand);
            }
        }

        private void Initialize()
        {
            // Init buttons/icons.
            const float iconsY = 177f;
            Components.Clear();
            HotKeyButtons = new HotKeyButton[HotKeyUtil.IconCount];
            float xPosition = 0f;
            var itemBackdrops = HotKeyUtil.ItemBackdrops;
            for (var i = 0; i < HotKeyButtons.Length; i++)
            {
                var position = new Vector2 { x = xPosition, y = iconsY };
                HotKeyButtons[i] = new HotKeyButton(itemBackdrops[i], position, i + 1);
                Components.Add(HotKeyButtons[i]);
                xPosition += HotKeyButton.buttonWidth;
            }

            EquippedButton = new HotKeyButton(itemBackdrops[0], new Vector2 { x = 0f, y = iconsY }, 0);
            EquippedButton.KeyLabel.Enabled = false;
            EquippedButton.Enabled = false;
            Components.Add(EquippedButton);
            initialized = true;
        }

        private void SetScale(Vector2 scale)
        {
            Scale = scale;
            for (var i = 0; i < HotKeyButtons.Length; i++)
                HotKeyButtons[i].SetScale(scale);
            EquippedButton.SetScale(scale);
        }

        private void SetEquipDelayTime(DaggerfallUnityItem lastRightHandItem, DaggerfallUnityItem lastLeftHandItem)
        {
            var delayTimeRight = 0;
            var delayTimeLeft = 0;
            var player = GameManager.Instance.PlayerEntity;
            var currentRightHandItem = player.ItemEquipTable.GetItem(EquipSlots.RightHand);
            var currentLeftHandItem = player.ItemEquipTable.GetItem(EquipSlots.LeftHand);

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

            GameManager.Instance.WeaponManager.EquipCountdownRightHand += delayTimeRight;
            GameManager.Instance.WeaponManager.EquipCountdownLeftHand += delayTimeLeft;
        }

        private void ShowEquipDelayMessage(float countDownValue, EquipSlots equipSlot)
        {
            if (countDownValue > 0)
            {
                var currentWeapon = playerEntity.ItemEquipTable.GetItem(equipSlot);
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
