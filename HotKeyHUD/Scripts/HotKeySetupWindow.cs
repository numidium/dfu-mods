using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeySetupWindow : DaggerfallPopupWindow
    {
        private const float topMarginHeight = 30f;
        private const float paddingWidth = 21f;
        private static Rect menuPopupRect = new Rect(21f, topMarginHeight, HotKeyButton.iconWidth * 3f, HotKeyButton.iconHeight * 3f);
        private static Rect itemListScrollerRect = new Rect(menuPopupRect.x + menuPopupRect.width + paddingWidth, topMarginHeight, 59f, 152f);
        private static Rect spellsListRect = new Rect(itemListScrollerRect.x + itemListScrollerRect.width + paddingWidth, topMarginHeight, 110f, 130f);
        private static Rect spellsListCutoutRect = new Rect(0f, 0f, 120f, 147f);
        private static Rect exitButtonCutoutRect = new Rect(216f, 149f, 43f, 15f);
        private readonly HotKeyDisplay hotKeyDisplay;
        private HotKeyMenuPopup hotKeyMenuPopup;
        private ItemListScroller itemListScroller;
        private ListBox spellsList;
        private int lastSelectedSlot = -1;
        private const string spellBookTextureFilename = "SPBK00I0.IMG";
        private Panel spellsListPanel;
        private Panel exitButtonPanel;
        private DaggerfallUnityItem hotKeyItem;
        private const string actionTypeSelect = "This item can be either Used or Equipped. Key as Use?"; // TODO: this is duplicated in HotkeyHUDInventory - remove duplicate
        private int slotNum;

        public HotKeySetupWindow(IUserInterfaceManager uiManager) : base(uiManager)
        {
            hotKeyDisplay = (HotKeyDisplay)DaggerfallUI.Instance.DaggerfallHUD.ParentPanel.Components.FirstOrDefault(x => x.GetType() == typeof(HotKeyDisplay));
        }

        public override void OnPush()
        {
            base.OnPush();
            if (IsSetup)
            {
                ResetItemsList();
                ResetSpellsList();
            }
        }

        public override void Update()
        {
            base.Update();
            hotKeyMenuPopup.HandleSlotSelect(ref lastSelectedSlot);
            // Toggle closed
            var keyDown = InputManager.Instance.GetAnyKeyDown();
            if (keyDown == HotKeyHUD.SetupMenuKey)
                CloseWindow();
        }

        protected override void Setup()
        {
            if (IsSetup)
                return;
            base.Setup();
            itemListScroller = new ItemListScroller(defaultToolTip)
            {
                Position = new Vector2(itemListScrollerRect.x, itemListScrollerRect.y),
                Size = new Vector2(itemListScrollerRect.width, itemListScrollerRect.height)
            };

            itemListScroller.OnItemClick += ItemListScroller_OnItemClick;

            var spellbookTexture = ImageReader.GetTexture(spellBookTextureFilename);
            spellsListPanel = new Panel()
            {
                Position = new Vector2(spellsListRect.x, spellsListRect.y),
                Size = new Vector2(spellsListRect.width, spellsListRect.height),
                BackgroundTexture = ImageReader.GetSubTexture(spellbookTexture, spellsListCutoutRect)
            };

            spellsList = new ListBox()
            {
                Position = new Vector2(6f, 13f),
                Size = new Vector2(spellsListRect.width - 13f, spellsListRect.height - 6f),
            };

            spellsList.OnSelectItem += SpellsList_OnSelectItem;

            exitButtonPanel = new Panel()
            {
                Position = new Vector2(277f, 185f),
                Size = new Vector2(exitButtonCutoutRect.width, exitButtonCutoutRect.height),
                BackgroundTexture = ImageReader.GetSubTexture(spellbookTexture, exitButtonCutoutRect)
            };

            hotKeyMenuPopup = new HotKeyMenuPopup(menuPopupRect.x, menuPopupRect.y, true);
            ResetItemsList();
            ResetSpellsList();
            ParentPanel.BackgroundColor = Color.clear;
            spellsListPanel.Components.Add(spellsList);
            NativePanel.Components.Add(itemListScroller);
            NativePanel.Components.Add(spellsListPanel);
            NativePanel.Components.Add(hotKeyMenuPopup);
            NativePanel.Components.Add(exitButtonPanel);
            NativePanel.Components.Add(new TextLabel()
            {
                Position = new Vector2(0f, 3f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = "Hot Key Assignment"
            });
            var exitButton = DaggerfallUI.AddButton(new Rect(exitButtonPanel.Position.x, exitButtonPanel.Position.y,
                exitButtonCutoutRect.width, exitButtonCutoutRect.height), NativePanel);
            exitButton.OnMouseClick += ExitButton_OnMouseClick;
            exitButton.ClickSound = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            IsSetup = true;
        }

        private void ExitButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            CloseWindow();
        }

        private void ItemListScroller_OnItemClick(DaggerfallUnityItem item)
        {
            KeyItem(item);
        }

        private void SpellsList_OnSelectItem()
        {
            var spellBook = GameManager.Instance.PlayerEntity.GetSpells();
            var spell = spellBook[spellsList.SelectedIndex];
            hotKeyDisplay.SetSpellAtSlot(in spell, hotKeyMenuPopup.SelectedSlot);
            hotKeyMenuPopup.SyncIcons();
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
        }

        private void ResetItemsList()
        {
            var itemCollection = GameManager.Instance.PlayerEntity.Items;
            var items = new List<DaggerfallUnityItem>();
            for (var i = 0; i < itemCollection.Count; i++)
                items.Add(itemCollection.GetItem(i));
            itemListScroller.Items = items;
        }

        private void ResetSpellsList()
        {
            spellsList.ClearItems();
            var spellbook = GameManager.Instance.PlayerEntity.GetSpells();
            foreach (var spell in spellbook)
                spellsList.AddItem(spell.Name);
        }

        // TODO: repeated in HotKeyHUDInventoryMenu - remove repetition
        private bool KeyItem(DaggerfallUnityItem item)
        {
            if (!GetProhibited(item) && item.currentCondition > 0) // Item must not be class-restricted or broken.
            {
                slotNum = hotKeyMenuPopup.SelectedSlot;
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
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                    hotKeyDisplay.SetItemAtSlot(hotKeyItem, slotNum);
                    hotKeyMenuPopup.SyncIcons();
                }

                return true;
            }

            return false;
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
        // TODO: repeated in HotKeyHUDInventoryMenu - remove repetition
        private static bool GetProhibited(DaggerfallUnityItem item)
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

        // TODO: repeated in HotKeyHUDInventoryMenu - remove repetition
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
