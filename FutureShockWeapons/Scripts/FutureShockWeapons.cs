using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FutureShock
{
    sealed public class FutureShockWeapons : MonoBehaviour
    {
        private static Mod mod;
        private bool componentAdded;
        private static HitScanWeapon hitScanGun;

        public static FutureShockWeapons Instance { get; private set; }
        public Type SaveDataType => typeof(FutureShockWeapons);
        public static string ModTitle => mod.Title;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<FutureShockWeapons>();
            //mod.SaveDataInterface = Instance;
            mod.LoadSettingsCallback = Instance.LoadSettings;
            /*
            var imgBsa = new BsaFile("F:\\dosgames\\futureshock\\doublepack\\Games\\The Terminator - Future Shock\\GAMEDATA\\MDMDIMGS.BSA", FileUsage.UseMemory, true);

            // UZI 9MM
            var data = imgBsa.GetRecordBytes(imgBsa.GetRecordIndex("WEAPON01.CFA"));
            using (var stream = File.Open("F:\\dosgames\\futureshock\\doublepack\\Games\\The Terminator - Future Shock\\GAMEDATA\\WEAPON01.CFA", FileMode.Create))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(data);
                }
            }
            */
        }

        void LoadSettings(ModSettings settings, ModSettingsChange change)
        {

        }

        private void Awake()
        {
            InitMod();
        }

        private void Update()
        {
            hitScanGun.IsFiring = InputManager.Instance.GetKey(KeyCode.Mouse1, false);
        }

        public static void InitMod()
        {
            //var settings = mod.GetSettings();
            var player = GameObject.FindGameObjectWithTag("Player");
            hitScanGun = player.AddComponent<HitScanWeapon>();
            Debug.Log("Future Shock Weapons initialized.");
        }
    }
}
