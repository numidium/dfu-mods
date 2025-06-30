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
        public Type SaveDataType => typeof(HKHSaveData);
        private KeyCode setupMenuKey;
        private KeyItem[] keyItems;
        private HKHInput hkhInput;
        private DaggerfallHUD daggerfallHud;
        private HKHDisplay hotKeyDisplay;
        private DaggerfallUnityItem equippedItem;
        private bool equipDelayDisabled;
        private HKHUtil.BlankHandler OnResetKeyItems { get; set; }
        private HKHUtil.ItemSetHandler OnSetKeyItem { get; set; }
        private HKHUtil.ItemUseHandler OnActivateKeyItem { get; set; }
        private HKHUtil.BlankHandler OnEquipDelay { get; set; }
        private EntityEffectManager playerEffectManager;
        private bool autoRecastEnabled;
        private EntityEffectBundle autoSpell;
        private bool inventoryEventsSet, spellbookEventsSet;

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
            var hotKeyMenuPopup = HKHMenuPopup.Instance;
            if (!hotKeyMenuPopup.Initialized)
                hotKeyMenuPopup.Initialize();
            ResetItems();
            return new HKHSaveData
            {
                payloadTypes = new List<PayloadType>(),
                forceUseSlots = new List<bool>(),
                itemUids = new List<ulong>(),
                spells = new List<EffectBundleSettings>()
            };
        }

        public object GetSaveData()
        {
            var data = new HKHSaveData
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
            var hotKeyMenuPopup = HKHMenuPopup.Instance;
            if (!hotKeyMenuPopup.Initialized)
                hotKeyMenuPopup.Initialize();
            // Clear items
            for (var i = 0; i < keyItems.Length; i++)
                SetKeyItem(i, null);
            var data = (HKHSaveData)saveData;
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
            hotKeyDisplay = HKHDisplay.Instance;
            hotKeyDisplay.Visibility = (HKHUtil.HUDVisibility)settings.GetValue<int>("Options", "HUD Visibility");
            switch (settings.GetValue<int>("Options", "HUD Scale"))
            {
                case 0:
                    hotKeyDisplay.ScaleMult = .5f;
                    break;
                case 1:
                    hotKeyDisplay.ScaleMult = .75f;
                    break;
                default:
                    hotKeyDisplay.ScaleMult = 1f;
                    break;
            }

            autoRecastEnabled = settings.GetValue<bool>("Options", "Auto Recast");
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
            hkhInput = gameObject.AddComponent<HKHInput>();
            if (!hkhInput)
            {
                Debug.Log($"{modName}: Fatal error - could not add input handler component.");
                Destroy(this);
                return;
            }

            // Load settings that require a restart.
            var settings = mod.GetSettings();
            HKHMenuPopup.OverrideMenus = settings.GetValue<bool>("Options", "Override Menus");
            LoadSettings(settings, new ModSettingsChange());
            Debug.Log($"{modName} initialized.");
            mod.IsReady = true;
            GameManager.Instance.PlayerEntity.OnDeath += OnPlayerDeath;
            if (HKHMenuPopup.OverrideMenus)
            {
                UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Inventory, typeof(HKHInventoryMenu));
                UIWindowFactory.RegisterCustomUIWindow(UIWindowType.SpellBook, typeof(HKHSpellbookWindow));
            }
            else
            {
                HKHSetupWindow.Instance.Localize = mod.Localize;
                HKHSetupWindow.Instance.OnKeyItem += HandleKeyItem;
                HKHSetupWindow.Instance.OnOpen += HandleSetupOpen;
                HKHSetupWindow.Instance.OnSetupClose += HandleSetupClose;
            }

            hotKeyDisplay = HKHDisplay.Instance;
            hotKeyDisplay.Localize = mod.Localize;
            daggerfallHud = DaggerfallUI.Instance.DaggerfallHUD;
            daggerfallHud.ParentPanel.Components.Add(HKHDisplay.Instance);
            keyItems = new KeyItem[itemCount];
            playerEffectManager = GameManager.Instance.PlayerEffectManager;
            // Set up events
            hkhInput.KeyDownHandler += HandleKeyDown;
            hkhInput.SpellAbortHandler += HandleSpellAbort;
            hkhInput.ReadyWeaponHandler += HandleReadyWeapon;
            OnResetKeyItems += hotKeyDisplay.ResetItemsHandler;
            OnSetKeyItem += hotKeyDisplay.HandleItemSet;
            OnSetKeyItem += HKHMenuPopup.Instance.HandleItemSet;
            OnActivateKeyItem += hotKeyDisplay.HandleItemActivate;
            OnEquipDelay += hotKeyDisplay.HandleEquipDelay;
        }

        private void Update()
        {
            hotKeyDisplay.Enabled = daggerfallHud.Enabled;
            if (!hotKeyDisplay.Initialized || !hotKeyDisplay.Enabled)
                return;
            // Item polling/updating
            switch (hotKeyDisplay.Visibility)
            {
                case HKHUtil.HUDVisibility.Full:
                    for (var i = 0; i < keyItems.Length; i++)
                    {
                        var keyItem = keyItems[i];
                        if (keyItem.Item is DaggerfallUnityItem dfuItem)
                        {
                            // Remove item from hotkeys if it is:
                            // 1. no longer in inventory
                            // 2. broken from use
                            // 3. a consumed stack
                            if (!dfuItem.IsStackable() || HKHUtil.IsBow(dfuItem))
                            {
                                if (!GameManager.Instance.PlayerEntity.Items.Contains(dfuItem.UID) || dfuItem.currentCondition <= 0)
                                {
                                    keyItems[i].Item = null;
                                    keyItems[i].ForceUse = false;
                                    RaiseKeyItemSet(new HKHUtil.ItemSetEventArgs(i, null, false));
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
                                RaiseKeyItemSet(new HKHUtil.ItemSetEventArgs(i, null, false));
                            }

                            hotKeyDisplay.UpdateItemDisplay(i, dfuItem);
                        }
                    }
                    break;
                case HKHUtil.HUDVisibility.Equipped:
                    var weaponManager = GameManager.Instance.WeaponManager;
                    var item = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(weaponManager.UsingRightHand ? EquipSlots.RightHand : EquipSlots.LeftHand);
                    if (item != equippedItem)
                    {
                        RaiseKeyItemSet(new HKHUtil.ItemSetEventArgs(HKHUtil.EquippedButtonIndex, item, false));
                        equippedItem = item;
                    }
                    if (equippedItem != null)
                        hotKeyDisplay.UpdateItemDisplay(HKHUtil.EquippedButtonIndex, equippedItem);
                    break;
                default:
                    break;
            }

            if (autoRecastEnabled && autoSpell != null && !playerEffectManager.HasReadySpell && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                playerEffectManager.SetReadySpell(autoSpell);

            if (!HKHMenuPopup.OverrideMenus)
                return;
            if (!inventoryEventsSet && DaggerfallUI.UIManager.TopWindow is HKHInventoryMenu hkhInventoryMenu)
            {
                hkhInventoryMenu.OnOpen += HandleInventoryMenuOpen;
                HandleInventoryMenuOpen(hkhInventoryMenu); // events need to be setup on first open
                hkhInventoryMenu.OnKeyItem += HandleKeyItem;
                hkhInventoryMenu.OnInventoryClose += HandleInventoryClose;
                inventoryEventsSet = true;
            }

            if (!spellbookEventsSet && DaggerfallUI.UIManager.TopWindow is HKHSpellbookWindow hkhSpellbookWindow)
            {
                hkhSpellbookWindow.OnOpen += HandleSpellbookOpen;
                HandleSpellbookOpen(hkhSpellbookWindow);
                hkhSpellbookWindow.OnSpellbookClose += HandleSpellbookClose;
                hkhSpellbookWindow.OnKeyItem += HandleKeyItem;
                spellbookEventsSet = true;
            }
        }

        private void OnPlayerDeath(DaggerfallEntity entity)
        {
            ResetItems();
        }

        private void HandleKeyItem(HKHUtil.KeyItemEventArgs args)
        {
            const string actionTypeSelectKey = "KeyAsUse";
            var equipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;
            // Show prompt if enchanted item can be either equipped or used.
            if (args.Item is DaggerfallUnityItem dfuItem &&
                dfuItem != keyItems[args.Popup.SelectedSlot].Item &&
                dfuItem.IsEnchanted &&
                equipTable.GetEquipSlot(dfuItem) != EquipSlots.None &&
                HKHUtil.GetEnchantedItemIsUseable(dfuItem))
            {
                var actionSelectDialog = new KeySelectMessageBox(DaggerfallUI.UIManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, mod.Localize(actionTypeSelectKey), args.PreviousWindow)
                {
                    Item = args.Item,
                    Slot = args.Slot
                };

                actionSelectDialog.OnButtonClick += ActionSelectDialog_OnButtonClick;
                actionSelectDialog.Show();
            }
            else
            {
                DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                SetKeyItem(args.Popup.SelectedSlot, args.Item);
            }
        }


        private void HandleInventoryMenuOpen(object sender)
        {
            var hkhInventoryMenu = sender as HKHInventoryMenu;
            hkhInput.KeyDownHandler += hkhInventoryMenu.HandleKeyDown;
            hkhInput.KeyUpHandler += hkhInventoryMenu.HandleKeyUp;
        }

        private void HandleInventoryClose(object sender, List<DaggerfallUnityItem> remoteItems)
        {
            // Remove discarded items from keyed buttons.
            for (var i = 0; i < keyItems.Length; i++)
            {
                if (keyItems[i].Item is DaggerfallUnityItem item && remoteItems.Contains(item))
                    SetKeyItem(i, null);
            }

            var hkhInventoryMenu = sender as HKHInventoryMenu;
            hkhInput.KeyDownHandler -= hkhInventoryMenu.HandleKeyDown;
            hkhInput.KeyUpHandler -= hkhInventoryMenu.HandleKeyUp;
        }

        private void HandleSpellbookOpen(object sender)
        {
            var hkhSpellbookWindow = sender as HKHSpellbookWindow;
            hkhInput.KeyDownHandler += hkhSpellbookWindow.HandleKeyDown;
            hkhInput.KeyUpHandler += hkhSpellbookWindow.HandleKeyUp;
        }

        private void HandleSpellbookClose(object sender)
        {
            var hkhSpellbookWindow = sender as HKHSpellbookWindow;
            hkhInput.KeyDownHandler -= hkhSpellbookWindow.HandleKeyDown;
            hkhInput.KeyUpHandler -= hkhSpellbookWindow.HandleKeyUp;
        }

        private void HandleSetupOpen()
        {
            hkhInput.KeyDownHandler += HKHSetupWindow.Instance.HandleKeyDown;
            hkhInput.KeyUpHandler += HKHSetupWindow.Instance.HandleKeyUp;
        }

        private void HandleSetupClose()
        {
            hkhInput.KeyDownHandler -= HKHSetupWindow.Instance.HandleKeyDown;
            hkhInput.KeyUpHandler -= HKHSetupWindow.Instance.HandleKeyUp;
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
            var keySetRaised = false;
            for (var i = 0; i < itemCount; i++)
            {
                var itemsAreEqual = (item is EffectBundleSettings spell1 && keyItems[i].Item is EffectBundleSettings spell2 && HKHUtil.CompareSpells(spell1, spell2)) ||
                    (item is DaggerfallUnityItem && item == keyItems[i].Item);
                if (i == index && !itemsAreEqual)
                {
                    keyItems[index].Item = item;
                    keyItems[index].ForceUse = forceUse;
                    continue;
                }

                if (RemoveDuplicateIfAt(i, itemsAreEqual))
                    keySetRaised = i == index;
            }

            if (!keySetRaised)
                RaiseKeyItemSet(new HKHUtil.ItemSetEventArgs(index, item, forceUse));
        }

        private bool RemoveDuplicateIfAt(int i, bool condition)
        {
            if (condition)
            {
                keyItems[i].Item = null;
                keyItems[i].ForceUse = false;
                RaiseKeyItemSet(new HKHUtil.ItemSetEventArgs(i, null, false));
                return true;
            }

            return false;
        }

        private void ActivateHotkeyItem(DaggerfallUnityItem item, bool forceUse)
        {
            // Do equip delay for weapons.
            var player = GameManager.Instance.PlayerEntity;
            var lastRightHandItem = player.ItemEquipTable.GetItem(EquipSlots.RightHand);
            var lastLeftHandItem = player.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            var equipTable = player.ItemEquipTable;
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
            // Assign to player effect manager as ready spell
            var playerEffectManager = GameManager.Instance.PlayerEffectManager;
            if (playerEffectManager && !GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
            {
                var readySpell = new EntityEffectBundle(spell, GameManager.Instance.PlayerEntityBehaviour);
                if (playerEffectManager.SetReadySpell(readySpell, spell.Tag == PlayerEntity.lycanthropySpellTag) && spell.TargetType != TargetTypes.CasterOnly)
                    autoSpell = readySpell;
            }
        }

        private void ActivateHotkeyItem(int index, object item, bool forceUse = false)
        {
            autoSpell = null;
            if (autoRecastEnabled)
                playerEffectManager.AbortReadySpell();
            if (item is DaggerfallUnityItem dfuItem)
                ActivateHotkeyItem(dfuItem, forceUse);
            else if (item is EffectBundleSettings spell)
                ActivateHotkeyItem(ref spell);
            RaiseItemActivate(new HKHUtil.ItemUseEventArgs(index, item));
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

        private void HandleKeyDown(KeyCode key)
        {
            if (DaggerfallUI.UIManager.TopWindow != daggerfallHud)
                return;
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9)
            {
                var index = key - KeyCode.Alpha1;
                ActivateHotkeyItem(index, keyItems[index].Item, keyItems[index].ForceUse);
            }

            if (!HKHMenuPopup.OverrideMenus && key == setupMenuKey && !GameManager.IsGamePaused &&
                !SaveLoadManager.Instance.LoadInProgress && DaggerfallUI.UIManager.WindowCount == 0)
            {
                DaggerfallUI.UIManager.PushWindow(HKHSetupWindow.Instance);
            }
        }

        private void HandleSpellAbort()
        {
            autoSpell = null;
            playerEffectManager.AbortReadySpell();
        }

        private void HandleReadyWeapon()
        {
            autoSpell = null;
        }

        private void RaiseResetKeyItems()
        {
            OnResetKeyItems?.Invoke();
        }

        private void RaiseKeyItemSet(HKHUtil.ItemSetEventArgs args)
        {
            OnSetKeyItem?.Invoke(args);
        }

        private void RaiseItemActivate(HKHUtil.ItemUseEventArgs args)
        {
            OnActivateKeyItem?.Invoke(args);
        }

        private void RaiseEquipDelay()
        {
            OnEquipDelay?.Invoke();
        }
    }
}
