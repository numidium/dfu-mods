using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Utility;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.UserInterface;

namespace HotKeyHUD
{
    public class HotKeyHUD : MonoBehaviour, IHasModSaveData
    {
        public const byte iconCount = 9;
        private const string baseInvTextureName = "INVE00I0.IMG";
        private const float iconWidth = 22f;
        private const float iconHeight = 22f;
        private static Mod mod;
        private bool componentAdded;
        private static Texture2D[] itemBackdrops;
        private static readonly Rect[] backdropCutouts = new Rect[]
        {
            new Rect(0, 10, iconWidth, iconHeight),  new Rect(23, 10, iconWidth, iconHeight),
            new Rect(0, 41, iconWidth, iconHeight),  new Rect(23, 41, iconWidth, iconHeight),
            new Rect(0, 72, iconWidth, iconHeight),  new Rect(23, 72, iconWidth, iconHeight),
            new Rect(0, 103, iconWidth, iconHeight), new Rect(23, 103, iconWidth, iconHeight),
            new Rect(0, 134, iconWidth, iconHeight), new Rect(23, 134, iconWidth, iconHeight),
        };

        // Mod settings
        public static bool HideHotbar { get; set; }
        public static bool OverrideMenus { get; set; }
        public static KeyCode SetupMenuKey { get; set; }
        public Type SaveDataType => typeof(HotKeyHUDSaveData);
        public static string ModTitle => mod.Title;

        // Shared resources
        public static HotKeyDisplay HUDDisplay { get; private set; }
        public static Texture2D[] ItemBackdrops
        {
            get
            {
                // Note: These textures live in memory as long as program is running.
                if (itemBackdrops == null)
                {
                    var inventoryTexture = ImageReader.GetTexture(baseInvTextureName);
                    itemBackdrops = new Texture2D[iconCount];
                    for (int i = 0; i < itemBackdrops.Length; i++)
                        itemBackdrops[i] = ImageReader.GetSubTexture(inventoryTexture, backdropCutouts[i], new DFSize(320, 200));
                }

                return itemBackdrops;
            }
        }

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            mod.SaveDataInterface = go.AddComponent<HotKeyHUD>();
        }

        void Awake()
        {
            InitMod();
            mod.IsReady = true;
            componentAdded = false;
        }

        private void Update()
        {
            var hud = DaggerfallUI.Instance.DaggerfallHUD;
            if (!componentAdded && hud != null)
            {
                if (OverrideMenus)
                {
                    UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Inventory, typeof(HotkeyHUDInventoryMenu));
                    UIWindowFactory.RegisterCustomUIWindow(UIWindowType.SpellBook, typeof(HotKeyHUDSpellbookWindow));
                }

                HUDDisplay = new HotKeyDisplay()
                {
                    AutoSize = AutoSizeModes.ResizeToFill,
                    Size = hud.NativePanel.Size
                };

                hud.NativePanel.Components.Add(HUDDisplay);
                componentAdded = true;
            }

            HUDDisplay.Enabled = hud.Enabled;
        }

        public object NewSaveData()
        {
            HUDDisplay.ResetButtons();
            return new HotKeyHUDSaveData
            {
                payloadTypes = new List<PayloadType>(),
                forceUseSlots = new List<bool>(),
                itemUids = new List<ulong>(),
                spells = new List<EffectBundleSettings>()
            };
        }

        public object GetSaveData()
        {
            var data = new HotKeyHUDSaveData
            {
                payloadTypes = new List<PayloadType>(),
                forceUseSlots = new List<bool>(),
                itemUids = new List<ulong>(),
                spells = new List<EffectBundleSettings>()
            };

            foreach (var button in HUDDisplay.HotKeyButtons)
            {
                if (button.Payload is DaggerfallUnityItem item)
                {
                    data.payloadTypes.Add(PayloadType.Item);
                    data.itemUids.Add(item.UID);
                }
                else if (button.Payload is EffectBundleSettings settings)
                {
                    data.payloadTypes.Add(PayloadType.Spell);
                    data.spells.Add(settings);
                }
                else
                    data.payloadTypes.Add(PayloadType.None);
                data.forceUseSlots.Add(button.ForceUse);
            }

            return data;
        }

        public void RestoreSaveData(object saveData)
        {
            // Clear buttons
            foreach (var button in HUDDisplay.HotKeyButtons)
                button.SetItem(null);
            var data = (HotKeyHUDSaveData)saveData;
            var player = GameManager.Instance.PlayerEntity;
            var itemIndex = 0;
            var spellIndex = 0;
            for (var i = 0; i < data.payloadTypes.Count; i++)
            {
                if (data.payloadTypes[i] == PayloadType.None)
                    HUDDisplay.SetItemAtSlot(null, i);
                else if (data.payloadTypes[i] == PayloadType.Item)
                {
                    var item = player.Items.GetItem(data.itemUids[itemIndex++]);
                    if (item != null)
                        HUDDisplay.SetItemAtSlot(item, i, data.forceUseSlots[i]);
                }
                else if (data.payloadTypes[i] == PayloadType.Spell)
                    HUDDisplay.SetSpellAtSlot(data.spells[spellIndex++], i);
            }
        }

        public static void InitMod()
        {
            var settings = mod.GetSettings();
            HideHotbar = settings.GetValue<bool>("Options", "Hide Hotbar");
            OverrideMenus = settings.GetValue<bool>("Options", "Override Menus");
            var menuKeyText = settings.GetValue<string>("Options", "Hotkey Setup Menu Key");
            if (Enum.TryParse(menuKeyText, out KeyCode result))
                SetupMenuKey = result;
            else
            {
                SetupMenuKey = KeyCode.Alpha0;
                Debug.Log("Hot Key HUD: Invalid setup menu keybind detected. Setting default.");
            }

            Debug.Log("Hot Key HUD initialized.");
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

        public static bool KeyItem(DaggerfallUnityItem item, ref int slotNum, IUserInterfaceManager uiManager, IUserInterfaceWindow prevWindow, HotKeyMenuPopup hotKeyMenuPopup,
            DaggerfallMessageBox.OnButtonClickHandler onButtonClickHandler, ref DaggerfallUnityItem hotKeyItem, HotKeyDisplay hotKeyDisplay)
        {
            const string actionTypeSelect = "This item can be either Used or Equipped. Key as Use?";
            if (!GetProhibited(item) && item.currentCondition > 0) // Item must not be class-restricted or broken.
            {
                slotNum = hotKeyMenuPopup.SelectedSlot;
                hotKeyItem = item;
                var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
                // Show prompt if enchanted item can be either equipped or used.
                if (item != hotKeyDisplay.GetItemAtSlot(slotNum) && item.IsEnchanted && equipTable.GetEquipSlot(item) != EquipSlots.None && GetEnchantedItemIsUseable(item))
                {
                    var actionSelectDialog = new DaggerfallMessageBox(uiManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, actionTypeSelect, prevWindow);
                    actionSelectDialog.OnButtonClick += onButtonClickHandler;
                    actionSelectDialog.Show();
                }
                else
                {
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                    hotKeyDisplay.SetItemAtSlot(hotKeyItem, slotNum);
                    hotKeyMenuPopup.SyncIcons();
                }

                return true;
            }

            return false;
        }
    }
}
