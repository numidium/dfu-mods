using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wenzil.Console;

namespace FutureShock
{
    sealed public class FutureShockWeapons : MonoBehaviour
    {
        enum WeaponAnimation
        {
            WEAPON01, // Uzi
            WEAPON02, // M16
            WEAPON03, // Machine Gun
            WEAPON04  // Shotgun
        }

        enum WeaponSound
        {
            SHOTS5,   // Uzi
            SHOTS2,   // M16 (SHOTS3 is identical)
            FASTGUN2, // Machine Gun
            SHTGUN    // Shotgun
        }

        enum FSWeapon
        {
            Uzi,
            M16,
            MachineGun,
            Shotgun
        }

        private static Mod mod;
        private bool componentAdded;
        private static HitScanWeapon hitScanGun;
        private const string gameDataPath = "F:\\dosgames\\futureshock\\doublepack\\Games\\The Terminator - Future Shock\\GAMEDATA\\";
        private static Dictionary<WeaponAnimation, Texture2D[]> textureBank;
        private static Dictionary<WeaponSound, AudioClip> soundBank;
        private static ConsoleController consoleController;
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
            consoleController = GameObject.Find("Console").GetComponent<ConsoleController>();
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
            if (consoleController.ui.isConsoleOpen || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress || DaggerfallUI.UIManager.WindowCount != 0)
                return;
            hitScanGun.IsFiring = InputManager.Instance.GetKey(KeyCode.Mouse1, false);
            if (InputManager.Instance.GetKeyDown(KeyCode.Alpha1))
                SetWeapon(FSWeapon.Uzi);
            else if (InputManager.Instance.GetKeyDown(KeyCode.Alpha2))
                SetWeapon(FSWeapon.M16);
            else if (InputManager.Instance.GetKeyDown(KeyCode.Alpha3))
                SetWeapon(FSWeapon.MachineGun);
            else if (InputManager.Instance.GetKeyDown(KeyCode.Alpha4))
                SetWeapon(FSWeapon.Shotgun);
        }

        public static void InitMod()
        {
            //var settings = mod.GetSettings();

            // Import Textures
            textureBank = new Dictionary<WeaponAnimation, Texture2D[]>();
            var shockPalette = new DFPalette($"{gameDataPath}SHOCK.COL");
            // Check for and/or load loose CFA files. Normally these will not exist until first run.
            foreach (WeaponAnimation textureName in Enum.GetValues(typeof(WeaponAnimation)))
                textureBank[textureName] = GetTextureAnimFromCfaFile($"{gameDataPath}{textureName}.CFA", shockPalette);
            using (var textureReader = new BsaReader($"{gameDataPath}MDMDIMGS.BSA"))
            {
                for (ushort textureIndex = 0; textureIndex < textureReader.IndexCount; textureIndex++)
                {
                    var fileName = textureReader.GetFileName(textureIndex);
                    var fileLength = textureReader.GetFileLength(textureIndex);
                    // Skip file if not in bank or already loaded.
                    if (!Enum.TryParse(Path.GetFileNameWithoutExtension(fileName), out WeaponAnimation weaponAnimation) || textureBank[weaponAnimation] != null)
                    {
                        textureReader.Reader.BaseStream.Seek(fileLength, SeekOrigin.Current);
                        continue;
                    }

                    var textureData = textureReader.Reader.ReadBytes(fileLength);
                    // Create a standalone CFA file that Interkarma's class can use.
                    var cfaPath = $"{gameDataPath}{fileName}";
                    using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(cfaPath, FileMode.Create)))
                    {
                        binaryWriter.Write(textureData);
                    }

                    textureBank[weaponAnimation] = GetTextureAnimFromCfaFile(cfaPath, shockPalette);
                }
            }

            // Import Sounds
            soundBank = new Dictionary<WeaponSound, AudioClip>();
            using (var soundBsa = new BsaReader($"{gameDataPath}MDMDSFXS.BSA"))
            {
                // Table ripped from Future Shock's memory during runtime
                byte[] noiseTable = { 0xDD, 0x83, 0x65, 0x57, 0xEA, 0x78, 0x08, 0x48, 0xB8, 0x01, 0x38, 0x94, 0x08, 0xDD, 0x3F, 0xC2, 0xBE, 0xAB, 0x76, 0xC6, 0x14 };
                for (ushort soundIndex = 0; soundIndex < soundBsa.IndexCount; soundIndex++)
                {
                    var fileName = soundBsa.GetFileName(soundIndex);
                    var fileLength = soundBsa.GetFileLength(soundIndex);
                    // Skip file if not in bank.
                    if (!Enum.TryParse(Path.GetFileNameWithoutExtension(fileName), out WeaponSound weaponSound))
                    {
                        soundBsa.Reader.BaseStream.Seek(fileLength, SeekOrigin.Current);
                        continue;
                    }

                    var soundData = soundBsa.Reader.ReadBytes(fileLength);
                    // De-noisify the sound data using Future Shock's noise table.
                    // Note: I believe that noisifying the sound files was intended as a data protection scheme.
                    var noiseTableInd = 0;
                    for (var i = 0; i < soundData.Length; i++)
                    {
                        soundData[i] -= noiseTable[noiseTableInd];
                        noiseTableInd = (noiseTableInd + 1) % noiseTable.Length;
                    }

                    var samples = new float[soundData.Length];
                    // Convert each sample byte to float in range -1 to 1.
                    const float conversionFactor = 1.0f / 128.0f;
                    for (var i = 0; i < soundData.Length; i++)
                        samples[i] = (soundData[i] - 128) * conversionFactor;
                    var clip = AudioClip.Create(fileName, fileLength, 1, 11025, false);
                    clip.SetData(samples, 0);
                    soundBank[weaponSound] = clip;
                }
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            hitScanGun = player.AddComponent<HitScanWeapon>();
            SetWeapon(FSWeapon.Uzi);
            Debug.Log("Future Shock Weapons initialized.");
        }

        private static Texture2D[] GetTextureAnimFromCfaFile(string path, DFPalette palette)
        {
            var cfaFile = new CfaFile() { Palette = palette };
            if (!cfaFile.Load(path, FileUsage.UseMemory, true))
                return null;
            var frameCount = cfaFile.GetFrameCount(0);
            var textureFrames = new Texture2D[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                var bitmap = cfaFile.GetDFBitmap(0, i);
                textureFrames[i] = new Texture2D(bitmap.Width, bitmap.Height)
                {
                    filterMode = FilterMode.Point
                };

                var colors = cfaFile.GetColor32(0, i, 0);
                textureFrames[i].SetPixels32(colors);
                textureFrames[i].Apply();
            }

            return textureFrames;
        }

        private static void SetWeapon(FSWeapon weapon)
        {
            hitScanGun.ResetAnimation();
            hitScanGun.UpdateRequested = true;
            switch (weapon)
            {
                case FSWeapon.Uzi:
                default:
                    hitScanGun.WeaponFrames = textureBank[WeaponAnimation.WEAPON01];
                    hitScanGun.HorizontalOffset = -0.3f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = soundBank[WeaponSound.SHOTS5];
                    hitScanGun.BulletDamage = 5;
                    hitScanGun.IsBurstFire = true;
                    break;
                case FSWeapon.M16:
                    hitScanGun.WeaponFrames = textureBank[WeaponAnimation.WEAPON02];
                    hitScanGun.HorizontalOffset = 0.1f;
                    hitScanGun.VerticalOffset = 0.05f;
                    hitScanGun.ShootSound = soundBank[WeaponSound.SHOTS2];
                    hitScanGun.BulletDamage = 10;
                    hitScanGun.IsBurstFire = true;
                    break;
                case FSWeapon.MachineGun:
                    hitScanGun.WeaponFrames = textureBank[WeaponAnimation.WEAPON03];
                    hitScanGun.HorizontalOffset = 0f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = soundBank[WeaponSound.FASTGUN2];
                    hitScanGun.BulletDamage = 15;
                    hitScanGun.IsBurstFire = true;
                    break;
                case FSWeapon.Shotgun:
                    hitScanGun.WeaponFrames = textureBank[WeaponAnimation.WEAPON04];
                    hitScanGun.HorizontalOffset = -0.2f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = soundBank[WeaponSound.SHTGUN];
                    hitScanGun.BulletDamage = 30;
                    hitScanGun.IsBurstFire = false;
                    break;
            }
        }
    }
}
