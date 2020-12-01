using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace HotKeyHUD
{
    public class HotKeyHUD : MonoBehaviour
    {
        static Mod mod;
        bool componentAdded;
        HotKeyDisplay displayComponent;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<HotKeyHUD>();
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
                displayComponent = new HotKeyDisplay()
                {
                    AutoSize = DaggerfallWorkshop.Game.UserInterface.AutoSizeModes.Scale,
                    Parent = hud.ParentPanel,
                    Size = hud.ParentPanel.Size
                };
                hud.ParentPanel.Components.Add(displayComponent);
                componentAdded = true;
            }
        }

        public static void InitMod()
        {
            //var settings = mod.GetSettings();
            Debug.Log("Hot Key HUD initialized.");
        }
    }
}
