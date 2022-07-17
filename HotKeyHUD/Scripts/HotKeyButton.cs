using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyButton : Panel
    {
        public const float iconWidth = 22f;
        public const float iconHeight = 22f;
        const float maxCondBarWidth = iconWidth - 3f;
        const int iconPanelSlot = 1;
        const int buttonKeyLabelSlot = 2;
        const int buttonStackLabelSlot = 3;
        const int buttonConditionBarSlot = 4;
        const float condBarHeight = 1f;
        const float iconsWidth = iconWidth * 10f;
        const float iconsY = 177f;
        readonly Vector2 originalPosition;

        public bool ForceUse { get; set; }
        public object Payload { get; set; }
        public byte PositionIndex { get; set; }
        public Panel Icon => (Panel)Components[iconPanelSlot];
        public TextLabel KeyLabel => (TextLabel)Components[buttonKeyLabelSlot];
        public TextLabel StackLabel => (TextLabel)Components[buttonStackLabelSlot];
        public Panel ConditionBar => (Panel)Components[buttonConditionBarSlot];
        private static Vector2 KeyLabelOriginalPos => new Vector2(1f, 1f);
        private static Vector2 StackLabelOriginalPos => new Vector2(1f, 1f);
        private static Vector2 CondBarOriginalPos => new Vector2(2f, iconHeight - 3f);

        public HotKeyButton(Texture2D backdrop, Vector2 position, int keyIndex)
        {
            // Button Backdrop
            BackgroundColor = Color.black;
            BackgroundTexture = backdrop;
            Size = new Vector2 { x = iconWidth, y = iconHeight };
            Position = position;
            originalPosition = position;

            // Payload Icon
            Components.Add(new Panel
            {
                BackgroundColor = Color.clear,
                AutoSize = AutoSizeModes.ScaleToFit,
                MaxAutoScale = 1f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle
            });

            // Key # Label
            Components.Add(new TextLabel
            {
                Position = KeyLabelOriginalPos,
                HorizontalAlignment = HorizontalAlignment.None,
                Text = keyIndex.ToString(),
                ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor,
                ShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos
            });

            // Stack # Label
            Components.Add(new TextLabel
            {
                Position = StackLabelOriginalPos,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Text = string.Empty,
                Enabled = false,
                ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor,
                ShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos
            });

            // Item condition bar
            Components.Add(new Panel
            {
                Position = CondBarOriginalPos,
                Size = new Vector2(maxCondBarWidth, condBarHeight),
                BackgroundColor = Color.green,
                Enabled = false
            });

            PositionIndex = (byte)(keyIndex - 1);
        }

        public override void Draw()
        {
            // It seems like I shouldn't have to do this. I think it's a bug in DFU.
            if (Icon.Size.y > Size.y)
                Icon.Size = Size * 0.9f;
            base.Draw();
        }

        public override void Update()
        {
            base.Update();
            if (StackLabel.Enabled && Payload is DaggerfallUnityItem item)
                StackLabel.Text = item.stackCount.ToString();
        }

        public void UpdateCondition(int percentage, in Vector2 scale)
        {
            // Shrink bar as value decreases.
            ConditionBar.Size = new Vector2(percentage / 100f * (maxCondBarWidth * scale.x), condBarHeight * scale.y);
            if (percentage >= 75)
                ConditionBar.BackgroundColor = Color.green;
            else if (percentage >= 25)
                ConditionBar.BackgroundColor = Color.yellow;
            else
                ConditionBar.BackgroundColor = Color.red;
        }

        public void SetItem(DaggerfallUnityItem item, bool forceUse = false)
        {
            // Toggle clear slot.
            if (item != null && item == Payload)
            {
                SetItem(null);
                return;
            }

            Payload = item;
            ForceUse = forceUse;
            if (item == null)
            {
                Icon.BackgroundTexture = null;
                ConditionBar.Enabled = false;
                StackLabel.Enabled = false;
            }
            else
            {
                var image = DaggerfallUnity.Instance.ItemHelper.GetInventoryImage(item);
                Icon.BackgroundTexture = image.texture;
                Icon.Size = new Vector2(image.width, image.height);
                StackLabel.Enabled = item.IsStackable();
                // I'm assuming there aren't any stackables with condition worth tracking.
                ConditionBar.Enabled = !StackLabel.Enabled;
            }
        }

        public void SetSpell(in EffectBundleSettings spell)
        {
            const float spellIconScale = .8f;
            // Toggle clear slot.
            if (Payload is EffectBundleSettings settings && HotKeyHUD.CompareSpells(spell, settings))
            {
                SetItem(null);
                return;
            }

            Payload = spell;
            Icon.BackgroundTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellIcon(spell.Icon);
            Icon.Size = new Vector2(Icon.Parent.Size.x * spellIconScale, Icon.Parent.Size.y * spellIconScale);
            ConditionBar.Enabled = false;
        }

        public void HandleItemHotkeyPress(DaggerfallUnityItem item)
        {
            var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
            var player = GameManager.Instance.PlayerEntity;
            List<DaggerfallUnityItem> unequippedList = null;
            // Toggle light source.
            if (item.IsLightSource)
                player.LightSource = (player.LightSource == item ? null : item);
            // Use enchanted item.
            if (item.IsEnchanted && (equipTable.GetEquipSlot(item) == EquipSlots.None || ForceUse))
            {
                GameManager.Instance.PlayerEffectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Used, item, player.Items);
                // Remove item if broken by use.
                if (item.currentCondition <= 0)
                    SetItem(null);
            }
            // Consume potion.
            else if (item.IsPotion)
            {
                GameManager.Instance.PlayerEffectManager.DrinkPotion(item);
                player.Items.RemoveOne(item);
                if (item.stackCount == 0) // Camel-case public fields? :)
                    SetItem(null);
            }
            // Toggle item unequipped.
            else if (equipTable.IsEquipped(item))
            {
                equipTable.UnequipItem(item);
                player.UpdateEquippedArmorValues(item, false);
            }
            // Remove broken item from menu.
            else if (item.currentCondition <= 0)
                SetItem(null);
            // Toggle item equipped.
            else
                unequippedList = equipTable.EquipItem(item);

            // Handle equipped armor and list of unequipped items.
            if (unequippedList != null)
            {
                foreach (DaggerfallUnityItem unequippedItem in unequippedList)
                    player.UpdateEquippedArmorValues(unequippedItem, false);
                player.UpdateEquippedArmorValues(item, true);
            }
        }

        public void HandleSpellHotkeyPress(ref EffectBundleSettings spell)
        {
            // Note: Copied from DaggerfallSpellBookWindow with slight modification
            // Lycanthropes cast for free
            bool noSpellPointCost = spell.Tag == PlayerEntity.lycanthropySpellTag;

            // Assign to player effect manager as ready spell
            EntityEffectManager playerEffectManager = GameManager.Instance.PlayerEffectManager;
            if (playerEffectManager && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                playerEffectManager.SetReadySpell(new EntityEffectBundle(spell, GameManager.Instance.PlayerEntityBehaviour), noSpellPointCost);
        }

        public void SetScale(Vector2 scale)
        {
            var position = new Vector2((float)Math.Round((160f - iconsWidth / 2f + originalPosition.x + 0.5f) * scale.x) + .5f, (float)Math.Round(iconsY * scale.y) + .5f);
            var size = new Vector2((float)Math.Round(iconWidth * scale.x + .5f), (float)Math.Round(iconHeight * scale.y) + .5f);
            Position = position;
            Size = size;
            KeyLabel.Scale = scale;
            KeyLabel.Position = new Vector2((float)Math.Round(KeyLabelOriginalPos.x * scale.x + .5f), (float)Math.Round(KeyLabelOriginalPos.y * scale.y + .5f));
            KeyLabel.TextScale = scale.x;
            StackLabel.Scale = scale;
            StackLabel.Position = new Vector2((float)Math.Round(StackLabelOriginalPos.x * scale.x + .5f), (float)Math.Round(StackLabelOriginalPos.y * scale.y + .5f));
            StackLabel.TextScale = scale.x;
            ConditionBar.Scale = scale;
            ConditionBar.Position = new Vector2((float)Math.Round(CondBarOriginalPos.x * scale.x + .5f), (float)Math.Round(CondBarOriginalPos.y * scale.y + .5f));
            ConditionBar.Size = new Vector2((float)Math.Round(ConditionBar.Size.x * scale.x + .5f), (float)Math.Round(ConditionBar.Size.y * scale.y + .5f));
        }
    }
}
