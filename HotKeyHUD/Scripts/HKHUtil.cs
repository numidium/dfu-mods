using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Utility;
using System;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HKHUtil
    {
        public sealed class KeyItemEventArgs : EventArgs
        {
            public object Item { get; private set; }
            public int Slot { get; private set; }
            public IUserInterfaceWindow PreviousWindow { get; private set; }
            public HKHMenuPopup Popup { get; private set; }

            public KeyItemEventArgs(object item, int slot, IUserInterfaceWindow previousWindow, HKHMenuPopup popup)
            {
                Item = item;
                Slot = slot;
                PreviousWindow = previousWindow;
                Popup = popup;
            }
        }

        public struct ItemUseEventArgs
        {
            public int Index { get; private set; }
            public object Item { get; private set; }
            public ItemUseEventArgs(int index, object item)
            {
                Index = index;
                Item = item;
            }
        }

        public struct ItemSetEventArgs
        {
            public int Index { get; private set; }
            public object Item { get; private set; }
            public bool ForceUse { get; private set; }
            public ItemSetEventArgs(int index, object item, bool forceUse)
            {
                Index = index;
                Item = item;
                ForceUse = forceUse;
            }
        }

        public enum HUDVisibility { Equipped, Full, None }
        public const int IconCount = 9;
        public const int EquippedButtonIndex = IconCount;
        public delegate void BlankHandler();
        public delegate void SenderHandler(object sender);
        public delegate void KeyCodeHandler(KeyCode keyCode);
        public delegate void ItemUseHandler(ItemUseEventArgs args);
        public delegate void ItemSetHandler(ItemSetEventArgs args);
        public delegate void KeyItemHandler(KeyItemEventArgs args);
        private const float iconWidth = 23f;
        private const float iconHeight = 22f;
        private const string baseInvTextureName = "INVE00I0.IMG";
        private static readonly Rect[] backdropCutouts = new Rect[]
        {
            new Rect(0, 10, iconWidth, iconHeight),  new Rect(23, 10, iconWidth, iconHeight),
            new Rect(0, 41, iconWidth, iconHeight),  new Rect(23, 41, iconWidth, iconHeight),
            new Rect(0, 72, iconWidth, iconHeight),  new Rect(23, 72, iconWidth, iconHeight),
            new Rect(0, 103, iconWidth, iconHeight), new Rect(23, 103, iconWidth, iconHeight),
            new Rect(0, 134, iconWidth, iconHeight), new Rect(23, 134, iconWidth, iconHeight),
        };

        private static Texture2D[] itemBackdrops;
        public static Texture2D[] ItemBackdrops
        {
            get
            {
                // Note: These textures live in memory as long as program is running.
                if (itemBackdrops == null)
                {
                    var inventoryTexture = ImageReader.GetTexture(baseInvTextureName);
                    itemBackdrops = new Texture2D[IconCount];
                    for (var i = 0; i < itemBackdrops.Length; i++)
                        itemBackdrops[i] = ImageReader.GetSubTexture(inventoryTexture, backdropCutouts[i], new DFSize(320, 200));
                }

                return itemBackdrops;
            }
        }

        public static bool CompareSpells(in EffectBundleSettings spell1, in EffectBundleSettings spell2)
        {
            // Performs a shallow compare.
            if (spell1.Version != spell2.Version ||
                spell1.BundleType != spell2.BundleType ||
                spell1.TargetType != spell2.TargetType ||
                spell1.ElementType != spell2.ElementType ||
                spell1.RuntimeFlags != spell2.RuntimeFlags ||
                spell1.Name != spell2.Name ||
                //spell1.IconIndex != spell2.IconIndex ||
                spell1.MinimumCastingCost != spell2.MinimumCastingCost ||
                spell1.NoCastingAnims != spell2.NoCastingAnims ||
                spell1.Tag != spell2.Tag ||
                spell1.StandardSpellIndex != spell2.StandardSpellIndex
                //spell1.Icon.index != spell2.Icon.index ||
                //spell1.Icon.key != spell2.Icon.key ||
                )
                return false;
            var effectsLength1 = spell1.Effects == null ? 0 : spell1.Effects.Length;
            var effectsLength2 = spell2.Effects == null ? 0 : spell2.Effects.Length;
            var legacyEffectsLength1 = spell1.LegacyEffects == null ? 0 : spell1.LegacyEffects.Length;
            var legacyEffectsLength2 = spell2.LegacyEffects == null ? 0 : spell2.LegacyEffects.Length;
            if (effectsLength1 != effectsLength2 ||
                legacyEffectsLength1 != legacyEffectsLength2)
                return false;
            return true;
        }

        // Adapted from DaggerfallInventoryWindow
        public static bool GetProhibited(DaggerfallUnityItem item)
        {
            var prohibited = false;
            var playerEntity = GameManager.Instance.PlayerEntity;

            if (item.ItemGroup == ItemGroups.Armor)
            {
                // Check for prohibited shield
                if (item.IsShield && ((1 << (item.TemplateIndex - (int)Armor.Buckler) & (int)playerEntity.Career.ForbiddenShields) != 0))
                    prohibited = true;

                // Check for prohibited armor type (leather, chain or plate)
                else if (!item.IsShield && (1 << (item.NativeMaterialValue >> 8) & (int)playerEntity.Career.ForbiddenArmors) != 0)
                    prohibited = true;

                // Check for prohibited material
                else if (((item.nativeMaterialValue >> 8) == 2)
                    && (1 << (item.NativeMaterialValue & 0xFF) & (int)playerEntity.Career.ForbiddenMaterials) != 0)
                    prohibited = true;
            }
            else if (item.ItemGroup == ItemGroups.Weapons)
            {
                // Check for prohibited weapon type
                if ((item.GetWeaponSkillUsed() & (int)playerEntity.Career.ForbiddenProficiencies) != 0)
                    prohibited = true;
                // Check for prohibited material
                else if ((1 << item.NativeMaterialValue & (int)playerEntity.Career.ForbiddenMaterials) != 0)
                    prohibited = true;
            }

            return prohibited;
        }

        public static bool GetEnchantedItemIsUseable(DaggerfallUnityItem item)
        {
            var enchantments = item.GetCombinedEnchantmentSettings();
            foreach (var enchantment in enchantments)
            {
                var effectTemplate = GameManager.Instance.EntityEffectBroker.GetEffectTemplate(enchantment.EffectKey);
                if (effectTemplate.HasEnchantmentPayloadFlags(EnchantmentPayloadFlags.Used))
                    return true;
            }

            return false;
        }

        public static bool IsBow(DaggerfallUnityItem item)
        {
            return item.ItemGroup == ItemGroups.Weapons && DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(item) == WeaponTypes.Bow;
        }
    }
}
