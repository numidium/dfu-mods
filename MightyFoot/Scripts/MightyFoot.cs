using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace MightyFoot
{
    public class MightyFoot : MonoBehaviour
    {
        static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<MightyFoot>();
        }

        void Awake()
        {
            InitMod();
            mod.IsReady = true;
        }

        public static void InitMod()
        {
            var settings = mod.GetSettings();
            var player = GameObject.FindGameObjectWithTag("Player");
            var behaviour = player.AddComponent<MightyFootBehaviour>();
            behaviour.BindText = settings.GetValue<string>("Controls", "Keybind");
            Debug.Log("Mighty Foot initialized.");
        }
    }
}
