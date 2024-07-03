using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace HotKeyHUD
{
    public sealed class HotKeyHUD : MonoBehaviour, IHasModSaveData
    {
        private static Mod mod;
        public static string ModTitle => mod.Title;
        private const string modName = "Hot Key HUD";
        public static HotKeyHUD Instance { get; private set; }
        public Type SaveDataType => typeof(HotKeyHUDSaveData);
        private KeyCode setupMenuKey;

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
            HotKeyDisplay.Instance.ResetButtons();
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

            foreach (var button in HotKeyDisplay.Instance.HotKeyButtons)
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
            var hotKeyMenuPopup = HotKeyMenuPopup.Instance;
            if (!hotKeyMenuPopup.Initialized)
                hotKeyMenuPopup.Initialize();
            // Clear buttons
            var hotKeyDisplay = HotKeyDisplay.Instance;
            for (var i = 0; i < hotKeyDisplay.HotKeyButtons.Length; i++)
                hotKeyDisplay.SetItemAtSlot(null, i);
            var data = (HotKeyHUDSaveData)saveData;
            var player = GameManager.Instance.PlayerEntity;
            var itemIndex = 0;
            var spellIndex = 0;
            for (var i = 0; i < data.payloadTypes.Count; i++)
            {
                if (data.payloadTypes[i] == PayloadType.None)
                    hotKeyDisplay.SetItemAtSlot(null, i);
                else if (data.payloadTypes[i] == PayloadType.Item)
                {
                    var item = player.Items.GetItem(data.itemUids[itemIndex++]);
                    if (item != null)
                        hotKeyDisplay.SetItemAtSlot(item, i, data.forceUseSlots[i]);
                }
                else if (data.payloadTypes[i] == PayloadType.Spell)
                    hotKeyDisplay.SetSpellAtSlot(data.spells[spellIndex++], i);
            }
        }

        // Load settings that can change during runtime.
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            HotKeyDisplay.Instance.Visibility = (HotKeyUtil.HUDVisibility)settings.GetValue<int>("Options", "HUD Visibility");
            var menuKeyText = settings.GetValue<string>("Options", "Hotkey Setup Menu Key");
            if (Enum.TryParse(menuKeyText, out KeyCode result))
                setupMenuKey = result;
            else
            {
                setupMenuKey = KeyCode.Alpha0;
                Debug.Log($"{modName}: Invalid setup menu keybind detected. Setting default.");
            }

            HotKeyDisplay.Instance.EquipDelayDisabled = settings.GetValue<bool>("Options", "Disable Equip Delay");
        }

        private void Start()
        {
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
                UIWindowFactory.RegisterCustomUIWindow(UIWindowType.SpellBook, typeof(HotKeyHUDSpellbookWindow));
            }
            else
                HotKeySetupWindow.Instance.Localize = mod.Localize;
            HotKeyDisplay.Instance.Localize = mod.Localize;
            DaggerfallUI.Instance.DaggerfallHUD.ParentPanel.Components.Add(HotKeyDisplay.Instance);
        }

        private void Update()
        {
            // Open alternate keying window on user command.
            if (!HotKeyMenuPopup.OverrideMenus && setupMenuKey != KeyCode.None && InputManager.Instance.GetAnyKeyDown() == setupMenuKey &&
                !GameManager.IsGamePaused && !SaveLoadManager.Instance.LoadInProgress && DaggerfallUI.UIManager.WindowCount == 0)
            {
                DaggerfallUI.Instance.UserInterfaceManager.PushWindow(HotKeySetupWindow.Instance);
            }

            HotKeyDisplay.Instance.Enabled = DaggerfallUI.Instance.DaggerfallHUD.Enabled;
        }

        private void OnPlayerDeath(DaggerfallWorkshop.Game.Entity.DaggerfallEntity entity)
        {
            HotKeyDisplay.Instance.ResetButtons();
        }
    }
}
