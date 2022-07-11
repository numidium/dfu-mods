using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
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
        const string actionTypeSelect = "This item can be either Used or Equipped. Key as Use?";

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
            hotKeyMenuPopup.ShowOrHide(ref lastSelectedSlot);
        }

        protected override void LocalItemListScroller_OnItemClick(DaggerfallUnityItem item, ActionModes actionMode)
        {
            var hotKeyDown = KeyCode.Alpha1 - 1;
            var input = InputManager.Instance;
            for (var i = 0; i <= (int)KeyCode.Alpha9; i++)
            {
                var key = KeyCode.Alpha1 + i;
                if (input.GetKeyDown(input.GetComboCode(key, KeyCode.Mouse0)))
                    hotKeyDown = key;
            }

            if (hotKeyDown >= KeyCode.Alpha1 && hotKeyDown <= KeyCode.Alpha9 &&
                !GetProhibited(item) && item.currentCondition > 0) // Item must not be class-restricted or broken.
            {
                slotNum = hotKeyDown - KeyCode.Alpha1;
                hotKeyItem = item;
                var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
                // Show prompt if enchanted item can be either equipped or used.
                if (item != hotKeyDisplay.GetItemAtSlot(slotNum) && item.IsEnchanted && equipTable.GetEquipSlot(item) != EquipSlots.None && GetEnchantedItemIsUseable(item))
                {
                    var actionSelectDialog = new DaggerfallMessageBox(uiManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, actionTypeSelect, this);
                    actionSelectDialog.OnButtonClick += ActionSelectDialog_OnButtonClick;
                    actionSelectDialog.Show();
                }
                else
                {
                    DaggerfallUI.Instance.PlayOneShot(DaggerfallWorkshop.SoundClips.ButtonClick);
                    hotKeyDisplay.SetItemAtSlot(hotKeyItem, slotNum);
                    hotKeyMenuPopup.SyncIcons();
                }
            }
            else
                base.LocalItemListScroller_OnItemClick(item, actionMode);
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

        // Adapted from DaggerfallInventoryWindow
        // Note: Prohibited check will be run twice if it fails here.
        private bool GetProhibited(DaggerfallUnityItem item)
        {
            var prohibited = false;

            if (item.ItemGroup == ItemGroups.Armor)
            {
                // Check for prohibited shield
                if (item.IsShield && ((1 << (item.TemplateIndex - (int)Armor.Buckler) & (int)PlayerEntity.Career.ForbiddenShields) != 0))
                    prohibited = true;

                // Check for prohibited armor type (leather, chain or plate)
                else if (!item.IsShield && (1 << (item.NativeMaterialValue >> 8) & (int)PlayerEntity.Career.ForbiddenArmors) != 0)
                    prohibited = true;

                // Check for prohibited material
                else if (((item.nativeMaterialValue >> 8) == 2)
                    && (1 << (item.NativeMaterialValue & 0xFF) & (int)PlayerEntity.Career.ForbiddenMaterials) != 0)
                    prohibited = true;
            }
            else if (item.ItemGroup == ItemGroups.Weapons)
            {
                // Check for prohibited weapon type
                if ((item.GetWeaponSkillUsed() & (int)PlayerEntity.Career.ForbiddenProficiencies) != 0)
                    prohibited = true;
                // Check for prohibited material
                else if ((1 << item.NativeMaterialValue & (int)PlayerEntity.Career.ForbiddenMaterials) != 0)
                    prohibited = true;
            }

            return prohibited;
        }

        private static bool GetEnchantedItemIsUseable(DaggerfallUnityItem item)
        {
            var enchantments = item.GetCombinedEnchantmentSettings();

            foreach (var enchantment in enchantments)
            {
                var effectTemplate = GameManager.Instance.EntityEffectBroker.GetEffectTemplate(enchantment.EffectKey);
                if (effectTemplate.HasEnchantmentPayloadFlags(DaggerfallWorkshop.Game.MagicAndEffects.EnchantmentPayloadFlags.Used))
                    return true;
            }

            return false;
        }
    }
}
