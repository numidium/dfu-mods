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
        private bool componentAdded;
        public static HotKeyHUD Instance { get; private set; }
        public Type SaveDataType => typeof(HotKeyHUDSaveData);
        public static string ModTitle => mod.Title;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<HotKeyHUD>();
            mod.SaveDataInterface = Instance;
            mod.LoadSettingsCallback = Instance.LoadSettings;
        }

        // Load settings that can change during runtime.
        void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            HotKeyUtil.Visibility = (HotKeyUtil.HUDVisibility)settings.GetValue<int>("Options", "HUD Visibility");
            var menuKeyText = settings.GetValue<string>("Options", "Hotkey Setup Menu Key");
            if (Enum.TryParse(menuKeyText, out KeyCode result))
                HotKeyUtil.SetupMenuKey = result;
            else
            {
                HotKeyUtil.SetupMenuKey = KeyCode.Alpha0;
                Debug.Log("Hot Key HUD: Invalid setup menu keybind detected. Setting default.");
            }
        }

        private void Awake()
        {
            // Load settings that require a restart.
            var settings = mod.GetSettings();
            HotKeyUtil.OverrideMenus = settings.GetValue<bool>("Options", "Override Menus");
            LoadSettings(settings, new ModSettingsChange());
            Debug.Log("Hot Key HUD initialized.");
            mod.IsReady = true;
            componentAdded = false;
        }

        private void Update()
        {
            var hud = DaggerfallUI.Instance.DaggerfallHUD;
            var hudDisplay = HotKeyDisplay.Instance;
            if (!componentAdded && hud != null)
            {
                if (HotKeyUtil.OverrideMenus)
                {
                    UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Inventory, typeof(HotkeyHUDInventoryMenu));
                    UIWindowFactory.RegisterCustomUIWindow(UIWindowType.SpellBook, typeof(HotKeyHUDSpellbookWindow));
                }

                hud.ParentPanel.Components.Add(hudDisplay);
                componentAdded = true;
            }

            // Open alternate keying window on user command.
            if (!HotKeyUtil.OverrideMenus && InputManager.Instance.GetAnyKeyDown() == HotKeyUtil.SetupMenuKey &&
                !GameManager.IsGamePaused && !SaveLoadManager.Instance.LoadInProgress && DaggerfallUI.UIManager.WindowCount == 0)
            {
                var uiManager = DaggerfallUI.Instance.UserInterfaceManager;
                uiManager.PushWindow(HotKeySetupWindow.Instance);
            }

            hudDisplay.Enabled = hud.Enabled;
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
            var hudDisplay = HotKeyDisplay.Instance;
            // Clear buttons
            for (var i = 0; i < hudDisplay.HotKeyButtons.Length; i++)
                hudDisplay.SetItemAtSlot(null, i);
            var data = (HotKeyHUDSaveData)saveData;
            var player = GameManager.Instance.PlayerEntity;
            var itemIndex = 0;
            var spellIndex = 0;
            for (var i = 0; i < data.payloadTypes.Count; i++)
            {
                if (data.payloadTypes[i] == PayloadType.None)
                    hudDisplay.SetItemAtSlot(null, i);
                else if (data.payloadTypes[i] == PayloadType.Item)
                {
                    var item = player.Items.GetItem(data.itemUids[itemIndex++]);
                    if (item != null)
                        hudDisplay.SetItemAtSlot(item, i, data.forceUseSlots[i]);
                }
                else if (data.payloadTypes[i] == PayloadType.Spell)
                    hudDisplay.SetSpellAtSlot(data.spells[spellIndex++], i);
            }
        }
    }
}
