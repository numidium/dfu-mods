using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Items;

namespace HotKeyHUD
{
    public class HotKeyHUD : MonoBehaviour, IHasModSaveData
    {
        static Mod mod;
        bool componentAdded;
        HotKeyDisplay displayComponent;

        public Type SaveDataType => typeof(HotKeyHUDSaveData);

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
                UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Inventory, typeof(HotkeyHUDInventoryMenu));
                UIWindowFactory.RegisterCustomUIWindow(UIWindowType.SpellBook, typeof(HotKeyHUDSpellbookWindow));
                displayComponent = new HotKeyDisplay()
                {
                    AutoSize = DaggerfallWorkshop.Game.UserInterface.AutoSizeModes.Scale,
                    Size = hud.ParentPanel.Size
                };
                hud.ParentPanel.Components.Add(displayComponent);
                componentAdded = true;
            }

            displayComponent.Enabled = hud.Enabled;
        }

        public static void InitMod()
        {
            //var settings = mod.GetSettings();
            Debug.Log("Hot Key HUD initialized.");
        }

        public object NewSaveData()
        {
            displayComponent.ResetButtons();

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

            var buttonList = displayComponent.ButtonList;
            foreach (var button in buttonList)
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
            var data = (HotKeyHUDSaveData)saveData;
            var player = GameManager.Instance.PlayerEntity;
            var itemIndex = 0;
            var spellIndex = 0;
            for (var i = 0; i < data.payloadTypes.Count; i++)
            {
                if (data.payloadTypes[i] == PayloadType.None)
                    displayComponent.SetItemAtSlot(null, i, true);
                else if (data.payloadTypes[i] == PayloadType.Item)
                {
                    var item = player.Items.GetItem(data.itemUids[itemIndex++]);
                    if (item != null)
                        displayComponent.SetItemAtSlot(item, i, data.forceUseSlots[i]);
                }
                else if (data.payloadTypes[i] == PayloadType.Spell)
                    displayComponent.SetSpellAtSlot(data.spells[spellIndex++], i, true);
            }
        }
    }
}
