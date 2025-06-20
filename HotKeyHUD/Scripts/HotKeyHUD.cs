using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HotKeyHUD : MonoBehaviour, IHasModSaveData
    {
        private static Mod mod;
        public static string ModTitle => mod.Title;
        private const string modName = "Hot Key HUD";
        private const int itemCount = 9;
        public static HotKeyHUD Instance { get; private set; }
        public Type SaveDataType => typeof(HotKeyHUDSaveData);
        private KeyCode setupMenuKey;
        private KeyItem[] keyItems;
        private HotKeyDisplay hotKeyDisplay;
        private DaggerfallUnityItem equippedItem;
        private bool equipDelayDisabled;
        private EventHandler OnResetKeyItems { get; set; }
        private EventHandler<ItemSetEventArgs> OnSetKeyItem { get; set; }
        private EventHandler<ItemUseEventArgs> OnActivateKeyItem { get; set; }
        private EventHandler OnEquipDelay { get; set; }

        private struct KeyItem
        {
            public object Item { get; set; }
            public bool ForceUse { get; set; }
        }

        private class KeySelectMessageBox : DaggerfallMessageBox
        {
            public int Slot { get; set; }
            public object Item { get; set; }
            public KeySelectMessageBox(IUserInterfaceManager uiManager, CommonMessageBoxButtons buttons, string text, IUserInterfaceWindow previous = null) : base(uiManager, buttons, text, previous)
            {
            }
        }

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<HotKeyHUD>();
            mod.LoadSettingsCallback = Instance.LoadSettings;
            mod.SaveDataInterface = Instance;
        }

        public object NewSaveData()
        {
            var hotKeyMenuPopup = HotKeyMenuPopup.Instance;
            if (!hotKeyMenuPopup.Initialized)
                hotKeyMenuPopup.Initialize();
            ResetItems();
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

            foreach (var keyItem in keyItems)
            {
                if (keyItem.Item is DaggerfallUnityItem item)
                {
                    data.payloadTypes.Add(PayloadType.Item);
                    data.itemUids.Add(item.UID);
                }
                else if (keyItem.Item is EffectBundleSettings settings)
                {
                    data.payloadTypes.Add(PayloadType.Spell);
                    data.spells.Add(settings);
                }
                else
                    data.payloadTypes.Add(PayloadType.None);
                data.forceUseSlots.Add(keyItem.ForceUse);
            }

            return data;
        }

        public void RestoreSaveData(object saveData)
        {
            var hotKeyMenuPopup = HotKeyMenuPopup.Instance;
            if (!hotKeyMenuPopup.Initialized)
                hotKeyMenuPopup.Initialize();
            // Clear items
            for (var i = 0; i < keyItems.Length; i++)
                SetKeyItem(i, null);
            var data = (HotKeyHUDSaveData)saveData;
            var player = GameManager.Instance.PlayerEntity;
            var itemIndex = 0;
            var spellIndex = 0;
            for (var i = 0; i < data.payloadTypes.Count; i++)
            {
                if (data.payloadTypes[i] == PayloadType.None)
                    SetKeyItem(i, null);
                else if (data.payloadTypes[i] == PayloadType.Item)
                {
                    var item = player.Items.GetItem(data.itemUids[itemIndex++]);
                    if (item != null)
                        SetKeyItem(i, item, data.forceUseSlots[i]);
                }
                else if (data.payloadTypes[i] == PayloadType.Spell)
                    SetKeyItem(i, data.spells[spellIndex++]);
            }
        }

        public object GetItem(int slot)
        {
            return keyItems[slot].Item;
        }

        // Load settings that can change during runtime.
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            hotKeyDisplay = HotKeyDisplay.Instance;
            hotKeyDisplay.Visibility = (HotKeyUtil.HUDVisibility)settings.GetValue<int>("Options", "HUD Visibility");
            hotKeyDisplay.AutoRecastEnabled = settings.GetValue<bool>("Options", "Auto Recast");
            var menuKeyText = settings.GetValue<string>("Options", "Hotkey Setup Menu Key");
            if (Enum.TryParse(menuKeyText, out KeyCode result))
                setupMenuKey = result;
            else
            {
                setupMenuKey = KeyCode.Alpha0;
                Debug.Log($"{modName}: Invalid setup menu keybind detected. Setting default.");
            }

            equipDelayDisabled = settings.GetValue<bool>("Options", "Disable Equip Delay");
        }

        private void Start()
        {
            var input = gameObject.AddComponent<HotKeyInput>();
            if (!input)
            {
                Debug.Log($"{modName}: Fatal error - could not add input handler component.");
                Destroy(this);
                return;
            }

            input.KeyDownHandler += HandleKeyDown;
            // Load settings that require a restart.
            var settings = mod.GetSettings();
            HotKeyMenuPopup.OverrideMenus = settings.GetValue<bool>("Options", "Override Menus");
            LoadSettings(settings, new ModSettingsChange());
            Debug.Log($"{modName} initialized.");
            mod.IsReady = true;
            GameManager.Instance.PlayerEntity.OnDeath += OnPlayerDeath;
            if (HotKeyMenuPopup.OverrideMenus)
            {
                UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Inventory, typeof(HotkeyHUDInventoryMenu));
                ((HotkeyHUDInventoryMenu)UIWindowFactory.GetInstance(UIWindowType.Inventory, DaggerfallUI.UIManager)).OnKeyItem += HandleKeyItem;
                ((HotkeyHUDInventoryMenu)UIWindowFactory.GetInstance(UIWindowType.Inventory, DaggerfallUI.UIManager)).OnInventoryClose += HandleInventoryClose;
                UIWindowFactory.RegisterCustomUIWindow(UIWindowType.SpellBook, typeof(HotKeyHUDSpellbookWindow));
                ((HotKeyHUDSpellbookWindow)UIWindowFactory.GetInstance(UIWindowType.SpellBook, DaggerfallUI.UIManager)).OnKeyItem += HandleKeyItem;
            }
            else
            {
                HotKeySetupWindow.Instance.Localize = mod.Localize;
                HotKeySetupWindow.Instance.OnKeyItem += HandleKeyItem;
            }

            hotKeyDisplay = HotKeyDisplay.Instance;
            hotKeyDisplay.Localize = mod.Localize;
            DaggerfallUI.Instance.DaggerfallHUD.ParentPanel.Components.Add(HotKeyDisplay.Instance);
            keyItems = new KeyItem[itemCount];
            // Set up events
            OnResetKeyItems += hotKeyDisplay.ResetItemsHandler;
            OnSetKeyItem += hotKeyDisplay.HandleItemSet;
            OnActivateKeyItem += hotKeyDisplay.HandleItemActivate;
            OnEquipDelay += hotKeyDisplay.HandleEquipDelay;
        }

        private void Update()
        {
            hotKeyDisplay.Enabled = DaggerfallUI.Instance.DaggerfallHUD.Enabled;
            if (!hotKeyDisplay.Initialized || !hotKeyDisplay.Enabled)
                return;
            // Item polling/updating
            switch (hotKeyDisplay.Visibility)
            {
                case HotKeyUtil.HUDVisibility.Full:
                    for (var i = 0; i < keyItems.Length; i++)
                    {
                        var keyItem = keyItems[i];
                        if (keyItem.Item is DaggerfallUnityItem dfuItem)
                        {
                            // Remove item from hotkeys if it is:
                            // 1. no longer in inventory
                            // 2. broken from use
                            // 3. a consumed stack
                            if (!dfuItem.IsStackable() || HotKeyUtil.IsBow(dfuItem))
                            {
                                if (!GameManager.Instance.PlayerEntity.Items.Contains(dfuItem.UID) || dfuItem.currentCondition <= 0)
                                {
                                    keyItems[i].Item = null;
                                    keyItems[i].ForceUse = false;
                                    RaiseKeyItemSet(new ItemSetEventArgs(i, null, false));
                                }
                                /*
                                // Scaling fix. Scaling seems to break if the parent panel height > width.
                                if (keyItem.Icon.InteriorHeight > HotKeyButton.buttonHeight * Scale.y)
                                    keyItem.Icon.Size *= .9f;
                                */
                            }
                            else if (dfuItem.IsStackable() && dfuItem.stackCount == 0)
                            {
                                keyItems[i].Item = null;
                                keyItems[i].ForceUse = false;
                                RaiseKeyItemSet(new ItemSetEventArgs(i, null, false));
                            }

                            hotKeyDisplay.UpdateItemDisplay(i, dfuItem);
                        }
                    }
                    break;
                case HotKeyUtil.HUDVisibility.Equipped:
                    var weaponManager = GameManager.Instance.WeaponManager;
                    var item = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(weaponManager.UsingRightHand ? EquipSlots.RightHand : EquipSlots.LeftHand);
                    if (item != equippedItem)
                    {
                        RaiseKeyItemSet(new ItemSetEventArgs(HotKeyUtil.EquippedButtonIndex, item, false));
                        equippedItem = item;
                    }
                    if (equippedItem != null)
                        hotKeyDisplay.UpdateItemDisplay(HotKeyUtil.EquippedButtonIndex, equippedItem);
                    break;
                default:
                    break;
            }
        }

        private void OnPlayerDeath(DaggerfallEntity entity)
        {
            ResetItems();
        }

        private void HandleKeyItem(object sender, KeyItemEventArgs args)
        {
            const string actionTypeSelectKey = "KeyAsUse";
            var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
            // Show prompt if enchanted item can be either equipped or used.
            if (args.Item is DaggerfallUnityItem dfuItem &&
                dfuItem != keyItems[args.Popup.SelectedSlot].Item &&
                dfuItem.IsEnchanted &&
                equipTable.GetEquipSlot(dfuItem) != EquipSlots.None &&
                HotKeyUtil.GetEnchantedItemIsUseable(dfuItem))
            {
                var actionSelectDialog = new KeySelectMessageBox(DaggerfallUI.UIManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, mod.Localize(actionTypeSelectKey), args.PreviousWindow);
                actionSelectDialog.OnButtonClick += ActionSelectDialog_OnButtonClick;
                actionSelectDialog.Show();
            }
            else
            {
                DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                SetKeyItem(args.Popup.SelectedSlot, args.Item);
            }
        }

        private void HandleInventoryClose(object sender, List<DaggerfallUnityItem> remoteItems)
        {
            // Remove discarded items from keyed buttons.
            for (var i = 0; i < keyItems.Length; i++)
            {
                if (keyItems[i].Item is DaggerfallUnityItem item && remoteItems.Contains(item))
                    SetKeyItem(i, null);
            }
        }

        private void ActionSelectDialog_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            sender.CloseWindow();
            var messageBox = (KeySelectMessageBox)sender;
            SetKeyItem(messageBox.Slot, messageBox.Item, sender.SelectedButton == DaggerfallMessageBox.MessageBoxButtons.Yes);
        }

        private void ResetItems()
        {
            for (var i = 0; i < keyItems.Length; i++)
            {
                keyItems[i].Item = null;
                keyItems[i].ForceUse = false;
            }

            RaiseResetKeyItems();
        }

        private void SetKeyItem(int index, object item, bool forceUse = false)
        {
            for (var i = 0; i < itemCount; i++)
            {
                if (i == index)
                    continue;
                if (RemoveDuplicateIfAt(index, i,
                    (item is EffectBundleSettings spell1 && keyItems[i].Item is EffectBundleSettings spell2 && HotKeyUtil.CompareSpells(spell1, spell2)) ||
                    (item is DaggerfallUnityItem && item == keyItems[i].Item)))
                    break;
                keyItems[index].Item = item;
                keyItems[index].ForceUse = forceUse;
            }

            RaiseKeyItemSet(new ItemSetEventArgs(index, item, forceUse));
        }

        private bool RemoveDuplicateIfAt(int index, int i, bool condition)
        {
            if (i != index && condition)
            {
                keyItems[i].Item = null;
                keyItems[i].ForceUse = false;
                return true;
            }

            return false;
        }

        private void ActivateHotkeyItem(DaggerfallUnityItem item, bool forceUse)
        {
            // Do equip delay for weapons.
            var lastRightHandItem = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            var lastLeftHandItem = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
            var player = GameManager.Instance.PlayerEntity;
            List<DaggerfallUnityItem> unequippedList = null;

            // Handle quest items
            // Note: copied from DaggerfallInventoryWindow
            // Handle quest items on use clicks
            if (item.IsQuestItem)
            {
                // Get the quest this item belongs to
                var quest = QuestMachine.Instance.GetQuest(item.QuestUID) ?? throw new Exception("DaggerfallUnityItem references a quest that could not be found.");

                // Get the Item resource from quest
                var questItem = quest.GetItem(item.QuestItemSymbol);

                // Use quest item
                if (!questItem.UseClicked && questItem.ActionWatching)
                {
                    questItem.UseClicked = true;

                    // Non-parchment and non-clothing items pop back to HUD so quest system has first shot at a custom click action in game world
                    // This is usually the case when actioning most quest items (e.g. a painting, bell, holy item, etc.)
                    // But when clicking a parchment or clothing item, this behaviour is usually incorrect (e.g. a letter to read)
                    if (!questItem.DaggerfallUnityItem.IsParchment && !questItem.DaggerfallUnityItem.IsClothing)
                    {
                        DaggerfallUI.Instance.PopToHUD();
                        return;
                    }
                }

                // Check for an on use value
                if (questItem.UsedMessageID != 0)
                {
                    // Display the message popup
                    quest.ShowMessagePopup(questItem.UsedMessageID, true);
                }
            }

            // Toggle light source.
            if (item.IsLightSource)
                player.LightSource = (player.LightSource == item ? null : item);
            // Refill lantern with oil
            // Note: Copied from DaggerfallInventoryWindow
            else if (item.ItemGroup == ItemGroups.UselessItems2 && item.TemplateIndex == (int)UselessItems2.Oil)
            {
                var lantern = player.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Lantern, allowQuestItem: false);
                if (lantern != null && lantern.currentCondition <= lantern.maxCondition - item.currentCondition)
                {   // Re-fuel lantern with the oil.
                    lantern.currentCondition += item.currentCondition;
                    player.Items.RemoveItem(item.IsAStack() ? player.Items.SplitStack(item, 1) : item);
                    DaggerfallUI.Instance.PlayOneShot(SoundClips.MakePotion); // Audio feedback when using oil.
                }
                else
                    DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("lightFull"), false, lantern);
            }
            // Use enchanted item.
            if (item.IsEnchanted && (equipTable.GetEquipSlot(item) == EquipSlots.None || forceUse))
            {
                var playerEffectManager = GameManager.Instance.PlayerEffectManager;
                if (playerEffectManager && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                    GameManager.Instance.PlayerEffectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Used, item, player.Items);
            }
            // Do drugs.
            // Note: Copied from DaggerfallInventoryWindow
            else if (item.ItemGroup == ItemGroups.Drugs)
            {
                // Drug poison IDs are 136 through 139. Template indexes are 78 through 81, so add to that.
                FormulaHelper.InflictPoison(player, player, (Poisons)item.TemplateIndex + 66, true);
                player.Items.RemoveItem(item);
            }
            // Consume potion.
            else if (item.IsPotion)
            {
                GameManager.Instance.PlayerEffectManager.DrinkPotion(item);
                player.Items.RemoveOne(item);
            }
            // Toggle item unequipped.
            else if (equipTable.IsEquipped(item))
            {
                equipTable.UnequipItem(item);
                player.UpdateEquippedArmorValues(item, false);
            }

            // Open the spellbook.
            else if (item.TemplateIndex == (int)MiscItems.Spellbook)
            {
                if (player.SpellbookCount() == 0)
                {
                    // Player has no spells
                    const int noSpellsTextId = 12;
                    var textTokens = DaggerfallUnity.Instance.TextProvider.GetRSCTokens(noSpellsTextId);
                    DaggerfallUI.MessageBox(textTokens);
                }
                else
                {
                    // Show spellbook
                    DaggerfallUI.UIManager.PostMessage(DaggerfallUIMessages.dfuiOpenSpellBookWindow);
                }
            }
            // Item is a mode of transportation.
            else if (item.ItemGroup == ItemGroups.Transportation)
            {
                if (GameManager.Instance.IsPlayerInside)
                    DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("cannotChangeTransportationIndoors"));
                else if (GameManager.Instance.PlayerController.isGrounded)
                {
                    var transportManager = GameManager.Instance.TransportManager;
                    var mode = transportManager.TransportMode;
                    if (item.TemplateIndex == (int)Transportation.Small_cart && mode != TransportModes.Cart)
                        transportManager.TransportMode = TransportModes.Cart;
                    else if (item.TemplateIndex == (int)Transportation.Horse && mode != TransportModes.Horse)
                        transportManager.TransportMode = TransportModes.Horse;
                    else
                        transportManager.TransportMode = TransportModes.Foot;
                }
            }
            // Otherwise, use a non-equippable.
            else if (equipTable.GetEquipSlot(item) == EquipSlots.None)
            {
                // Try to use a delegate that may have been registered by a mod.
                if (DaggerfallUnity.Instance.ItemHelper.GetItemUseHandler(item.TemplateIndex, out ItemHelper.ItemUseHandler itemUseHandler))
                    itemUseHandler(item, player.Items);
                // Handle normal items
                else if (item.ItemGroup == ItemGroups.Books && !item.IsArtifact)
                {
                    DaggerfallUI.Instance.BookReaderWindow.OpenBook(item);
                    if (DaggerfallUI.Instance.BookReaderWindow.IsBookOpen)
                        DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenBookReaderWindow);
                    else
                        DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("bookUnavailable"));
                }
            }
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

            if (!equipDelayDisabled && item.ItemGroup == ItemGroups.Weapons || item.IsShield)
            {
                SetEquipDelayTime(lastRightHandItem, lastLeftHandItem);
                RaiseEquipDelay();
            }
        }

        private void ActivateHotkeyItem(ref EffectBundleSettings spell)
        {
            // Note: Copied from DaggerfallSpellBookWindow with slight modification
            // Lycanthropes cast for free
            bool noSpellPointCost = spell.Tag == PlayerEntity.lycanthropySpellTag;

            // Assign to player effect manager as ready spell
            var playerEffectManager = GameManager.Instance.PlayerEffectManager;
            if (playerEffectManager && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                playerEffectManager.SetReadySpell(new EntityEffectBundle(spell, GameManager.Instance.PlayerEntityBehaviour), noSpellPointCost);
        }

        private void ActivateHotkeyItem(int index, object item, bool forceUse = false)
        {
            if (item is DaggerfallUnityItem dfuItem)
                ActivateHotkeyItem(dfuItem, forceUse);
            else if (item is EffectBundleSettings spell)
                ActivateHotkeyItem(ref spell);
            RaiseItemActivate(new ItemUseEventArgs(index, item));
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

        private void HandleKeyDown(object sender, KeyCode key)
        {
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9)
            {
                var index = key - KeyCode.Alpha1;
                ActivateHotkeyItem(index, keyItems[index].Item, keyItems[index].ForceUse);
            }

            if (!HotKeyMenuPopup.OverrideMenus && key == setupMenuKey && !GameManager.IsGamePaused &&
                !SaveLoadManager.Instance.LoadInProgress && DaggerfallUI.UIManager.WindowCount == 0)
            {
                DaggerfallUI.Instance.UserInterfaceManager.PushWindow(HotKeySetupWindow.Instance);
            }
        }

        private void RaiseResetKeyItems()
        {
            OnResetKeyItems?.Invoke(this, null);
        }

        private void RaiseKeyItemSet(ItemSetEventArgs args)
        {
            OnSetKeyItem?.Invoke(this, args);
        }

        private void RaiseItemActivate(ItemUseEventArgs args)
        {
            OnActivateKeyItem?.Invoke(this, args);
        }

        private void RaiseEquipDelay()
        {
            OnEquipDelay?.Invoke(this, null);
        }
    }
}
