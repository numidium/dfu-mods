using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyDisplay : Panel
    {
        public const byte iconCount = 9;
        const string baseInvTextureName = "INVE00I0.IMG";
        const float iconWidth = 22f;
        const float iconsWidth = iconWidth * 10f;
        const float iconHeight = 22f;
        const float iconsY = 177f;
        bool initialized = false;
        HotKeyButton[] hotKeyButtons;
        readonly Texture2D[] itemBackdrops;
        readonly Rect[] backdropCutouts = new Rect[]
        {
            new Rect(0, 10, iconWidth, iconHeight),  new Rect(23, 10, iconWidth, iconHeight),
            new Rect(0, 41, iconWidth, iconHeight),  new Rect(23, 41, iconWidth, iconHeight),
            new Rect(0, 72, iconWidth, iconHeight),  new Rect(23, 72, iconWidth, iconHeight),
            new Rect(0, 103, iconWidth, iconHeight), new Rect(23, 103, iconWidth, iconHeight),
            new Rect(0, 134, iconWidth, iconHeight), new Rect(23, 134, iconWidth, iconHeight),
        };
        Vector2[] originalPositions;
        DaggerfallUnityItem lastRightHandItem;
        DaggerfallUnityItem lastLeftHandItem;

        public List<HotKeyButton> ButtonList => hotKeyButtons.ToList();

        public HotKeyDisplay() : base()
        {
            Enabled = false;

            // Init textures
            var inventoryTexture = ImageReader.GetTexture(baseInvTextureName);
            itemBackdrops = new Texture2D[iconCount];
            for (int i = 0; i < itemBackdrops.Length; i++)
                itemBackdrops[i] = ImageReader.GetSubTexture(inventoryTexture, backdropCutouts[i], new DFSize(320, 200));
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
                if (button.Payload is DaggerfallUnityItem item)
                    button.UpdateCondition(item.ConditionPercentage, Scale);
                else
                    button.UpdateCondition(0, new Vector2(0,0));
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
            // Toggle clear slot.
            if (item != null && item == hotKeyButtons[index].Payload && forceUse == hotKeyButtons[index].ForceUse)
            {
                SetItemAtSlot(null, index);
                return;
            }

            hotKeyButtons[index].Payload = item;
            hotKeyButtons[index].ForceUse = forceUse;
            var icon = hotKeyButtons[index].Icon;
            if (item == null)
            {
                icon.BackgroundTexture = null;
                hotKeyButtons[index].UpdateCondition(0, Scale);
            }
            else
            {
                // If already in hotbar, delete from old slot.
                for (var i = 0; i < hotKeyButtons.Length; i++)
                    if (i != index && hotKeyButtons[i].Payload == item)
                    {
                        SetItemAtSlot(null, i);
                        break;
                    }
                var image = DaggerfallUnity.Instance.ItemHelper.GetInventoryImage(item);
                icon.BackgroundTexture = image.texture;
                icon.Size = new Vector2(image.width, image.height);
            }
        }

        public void SetSpellAtSlot(in EffectBundleSettings spell, int index, bool suppressNotif = false)
        {
            if (hotKeyButtons[index].Payload is EffectBundleSettings settings && spell.Equals(settings))
            {
                SetItemAtSlot(null, index);
                return;
            }

            hotKeyButtons[index].Payload = spell;
            hotKeyButtons[index].Icon.BackgroundTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellIcon(spell.Icon);
        }

        public void ResetButtons()
        {
            if (!initialized)
                return;
            var i = 0;
            foreach (var button in hotKeyButtons)
                SetItemAtSlot(null, i++);
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
                HandleSpellHotkeyPress(ref spell);
        }

        private void HandleItemHotkeyPress(DaggerfallUnityItem item, int index)
        {
            var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
            var player = GameManager.Instance.PlayerEntity;
            List<DaggerfallUnityItem> unequippedList = null;
            // Toggle light source.
            if (item.IsLightSource)
                player.LightSource = (player.LightSource == item ? null : item);
            // Use enchanted item.
            if (item.IsEnchanted && (equipTable.GetEquipSlot(item) == EquipSlots.None || hotKeyButtons[index].ForceUse))
            {
                GameManager.Instance.PlayerEffectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Used, item, player.Items);
                // Remove item if broken by use.
                if (item.currentCondition <= 0)
                    SetItemAtSlot(null, index);
            }
            // Consume potion.
            else if (item.IsPotion)
            {
                GameManager.Instance.PlayerEffectManager.DrinkPotion(item);
                player.Items.RemoveOne(item);
                if (item.stackCount == 0) // Camel-case public fields? :)
                    SetItemAtSlot(null, index);
            }
            // Toggle item unequipped.
            else if (equipTable.IsEquipped(item))
            {
                equipTable.UnequipItem(item);
                player.UpdateEquippedArmorValues(item, false);
            }
            // Remove broken item from menu.
            else if (item.currentCondition <= 0)
                SetItemAtSlot(null, index);
            // Toggle item equipped.
            else
                unequippedList = equipTable.EquipItem(item);

            // Do equip delay for weapons.
            if (item.ItemGroup == ItemGroups.Weapons)
            {
                SetEquipDelayTime();
                var weaponManager = GameManager.Instance.WeaponManager;
                // Show "equipping" message if a delay was added.
                ShowEquipDelayMessage(weaponManager.EquipCountdownRightHand, EquipSlots.RightHand);
                ShowEquipDelayMessage(weaponManager.EquipCountdownLeftHand, EquipSlots.LeftHand);
            }

            // Handle equipped armor and list of unequipped items.
            if (unequippedList != null)
            {
                foreach (DaggerfallUnityItem unequippedItem in unequippedList)
                    player.UpdateEquippedArmorValues(unequippedItem, false);
                player.UpdateEquippedArmorValues(item, true);
            }
        }

        private void HandleSpellHotkeyPress(ref EffectBundleSettings spell)
        {
            // Note: Copied from DaggerfallSpellBookWindow with slight modification
            // Lycanthropes cast for free
            bool noSpellPointCost = spell.Tag == PlayerEntity.lycanthropySpellTag;

            // Assign to player effect manager as ready spell
            EntityEffectManager playerEffectManager = GameManager.Instance.PlayerEffectManager;
            if (playerEffectManager)
                playerEffectManager.SetReadySpell(new EntityEffectBundle(spell, GameManager.Instance.PlayerEntityBehaviour), noSpellPointCost);
        }

        private void Initialize()
        {
            // Init buttons/icons.
            Components.Clear();
            hotKeyButtons = new HotKeyButton[iconCount];
            originalPositions = new Vector2[iconCount];
            float xPosition = 0f;
            for (int i = 0; i < hotKeyButtons.Length; i++)
            {
                var size = new Vector2 { x = iconWidth, y = iconHeight };
                var position = new Vector2 { x = xPosition, y = iconsY };
                hotKeyButtons[i] = new HotKeyButton(itemBackdrops[i], size, position, i + 1);
                xPosition += iconWidth;
                Components.Add(hotKeyButtons[i].Panel);
                originalPositions[i] = hotKeyButtons[i].Position;
            }

            // Init equip/unequip delay.
            var player = GameManager.Instance.PlayerEntity;
            lastRightHandItem = player.ItemEquipTable.GetItem(EquipSlots.RightHand);
            lastLeftHandItem = player.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            initialized = true;
        }

        private void SetScale(Vector2 scale)
        {
            Scale = scale;
            for (int i = 0; i < hotKeyButtons.Length; i++)
            {
                var position = new Vector2((float)Math.Round((160f - iconsWidth / 2f + originalPositions[i].x + 0.5f) * scale.x) + .5f, (float)Math.Round(iconsY * scale.y) + .5f);
                var size = new Vector2((float)Math.Round(iconWidth * scale.x + .5f), (float)Math.Round(iconHeight * scale.y) + .5f);
                hotKeyButtons[i].Position = position;
                hotKeyButtons[i].Size = size;
                hotKeyButtons[i].Icon.Size = size;
                hotKeyButtons[i].Label.Position = new Vector2((float)Math.Round(scale.x + .5f), (float)Math.Round(scale.y + .5f));
                hotKeyButtons[i].ConditionBar.Position = new Vector2((float)Math.Round(scale.x + .5f), (float)Math.Round(iconHeight * scale.y - 2f * scale.y + .5f));
                hotKeyButtons[i].ConditionBar.Size = new Vector2((float)Math.Round((iconWidth - 3f) * scale.x + .5f), (float)Math.Round(scale.y + .5f));
            }
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
