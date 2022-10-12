using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
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
    public sealed class FutureShockWeapons : MonoBehaviour
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
            SHOTS2,   // M16 (SHOTS3 is seemingly identical)
            FASTGUN2, // Machine Gun
            SHTGUN,   // Shotgun
            SGCOCK1,  // Shotgun equip
            SGCOCK2,  // M16/MG equip
            UZICOCK3  // Uzi equip
        }

        enum FSWeapon
        {
            Uzi,
            M16,
            MachineGun,
            Shotgun
        }

        private static Mod mod;
        private static HitScanWeapon hitScanGun;
        private static Dictionary<WeaponAnimation, Texture2D[]> textureBank;
        private static Dictionary<WeaponSound, AudioClip> soundBank;
        private static ConsoleController consoleController;
        private static DaggerfallUnityItem lastEquippedRight;
        private static DaggerfallUnityItem equippedRight;
        private static bool ShowWeapon;
        private static bool isLoadJustFinished = false;
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

        // Load settings that can change during runtime.
        void LoadSettings(ModSettings settings, ModSettingsChange change)
        {

        }

        private void Awake()
        {
            var settings = mod.GetSettings();
            var path = settings.GetValue<string>("Options", "FutureShock GAMEDATA path");
            //LoadSettings(settings, new ModSettingsChange());
            if (InitMod(path))
                Debug.Log("Future Shock Weapons initialized.");
            else
            {
                Debug.Log("ERROR: Future Shock Weapons failed to initialize.");
                var player = GameObject.FindGameObjectWithTag("Player");
                player.AddComponent<ErrorNotificationBehaviour>();
                Destroy(this);
            }
        }

        private void Update()
        {
            var gameManager = GameManager.Instance;
            equippedRight = gameManager.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            hitScanGun.PairedItem = equippedRight;
            if (consoleController.ui.isConsoleOpen || GameManager.IsGamePaused || DaggerfallUI.UIManager.WindowCount != 0)
                return;
            if (SaveLoadManager.Instance.LoadInProgress)
            {
                isLoadJustFinished = true;
                return;
            }

            if (isLoadJustFinished)
            {
                // Prevent playing equip sound on load.
                lastEquippedRight = equippedRight;
                isLoadJustFinished = false;
            }

            if (equippedRight != null && equippedRight.currentCondition <= 0)
            {
                hitScanGun.IsFiring = false;
                ShowWeapon = false;
                hitScanGun.IsHolstered = true;
                return;
            }

            hitScanGun.IsFiring = !hitScanGun.IsHolstered && InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon);
            if (InputManager.Instance.ActionStarted(InputManager.Actions.ReadyWeapon) && IsGun(equippedRight) && !hitScanGun.IsFiring)
                ShowWeapon = !ShowWeapon;
            if (!ShowWeapon)
                hitScanGun.IsHolstered = true;
            else if (hitScanGun.IsHolstered && gameManager.WeaponManager.EquipCountdownRightHand <= 0)
            {
                hitScanGun.IsHolstered = false;
                hitScanGun.PlayEquipSound();
            }
        }

        private void OnGUI()
        {
            // When unsheathing, immediately re-sheathe weapon and use HitScanGun in place of FPSWeapon
            var gameManager = GameManager.Instance;
            var equipChanged = false;
            if (lastEquippedRight != equippedRight)
                equipChanged = true;
            if (IsGun(equippedRight))
            {
                var lastNonGunSheathed = gameManager.WeaponManager.Sheathed;
                if (!lastNonGunSheathed)
                    gameManager.WeaponManager.SheathWeapons();
                if (equipChanged)
                {
                    ShowWeapon = (!IsGun(lastEquippedRight) && !lastNonGunSheathed) || !hitScanGun.IsHolstered;
                    SetWeapon(GetGunFromMaterial(equippedRight.NativeMaterialValue));
                    hitScanGun.PlayEquipSound();
                    hitScanGun.IsHolstered = true;
                }
            }
            else if (!hitScanGun.IsHolstered)
            {
                hitScanGun.IsHolstered = true;
                ShowWeapon = false;
                gameManager.WeaponManager.Sheathed = false;
            }

            if (equipChanged)
                lastEquippedRight = equippedRight;
        }

        private static bool IsGun(DaggerfallUnityItem item) => item != null && item.TemplateIndex == ItemFSGun.customTemplateIndex;

        public static bool InitMod(string gameDataPath)
        {
            // Import Textures
            textureBank = new Dictionary<WeaponAnimation, Texture2D[]>();
            var shockPalette = new DFPalette($"{gameDataPath}SHOCK.COL");
            // Check for and/or load loose CFA files. Normally these will not exist until first run.
            foreach (WeaponAnimation textureName in Enum.GetValues(typeof(WeaponAnimation)))
                textureBank[textureName] = GetTextureAnimFromCfaFile($"{gameDataPath}{textureName}.CFA", shockPalette);
            using (var textureReader = new BsaReader($"{gameDataPath}MDMDIMGS.BSA"))
            {
                if (textureReader.Reader == null)
                {
                    Debug.Log("Could not load MDMDIMGS.BSA from specified path.");
                    return false;
                }

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
            using (var soundReader = new BsaReader($"{gameDataPath}MDMDSFXS.BSA"))
            {
                if (soundReader.Reader == null)
                {
                    Debug.Log("Could not load MDMDSFXS.BSA from specified path.");
                    return false;
                }

                // Table ripped from Future Shock's memory during runtime
                byte[] noiseTable = { 0xDD, 0x83, 0x65, 0x57, 0xEA, 0x78, 0x08, 0x48, 0xB8, 0x01, 0x38, 0x94, 0x08, 0xDD, 0x3F, 0xC2, 0xBE, 0xAB, 0x76, 0xC6, 0x14 };
                for (ushort soundIndex = 0; soundIndex < soundReader.IndexCount; soundIndex++)
                {
                    var fileName = soundReader.GetFileName(soundIndex);
                    var fileLength = soundReader.GetFileLength(soundIndex);
                    // Skip file if not in bank.
                    if (!Enum.TryParse(Path.GetFileNameWithoutExtension(fileName), out WeaponSound weaponSound))
                    {
                        soundReader.Reader.BaseStream.Seek(fileLength, SeekOrigin.Current);
                        continue;
                    }

                    var soundData = soundReader.Reader.ReadBytes(fileLength);
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
            SetWeapon((int)WeaponMaterialTypes.Iron);
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemFSGun.customTemplateIndex, ItemGroups.Weapons, typeof(ItemFSGun));
            return true;
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

        private static FSWeapon GetGunFromMaterial(int material)
        {
            switch ((WeaponMaterialTypes)material)
            {
                case WeaponMaterialTypes.Iron:
                case WeaponMaterialTypes.Steel:
                case WeaponMaterialTypes.Silver:
                    return FSWeapon.Uzi;
                case WeaponMaterialTypes.Elven:
                case WeaponMaterialTypes.Dwarven:
                    return FSWeapon.M16;
                case WeaponMaterialTypes.Mithril:
                case WeaponMaterialTypes.Adamantium:
                    return FSWeapon.Shotgun;
                default:
                    return FSWeapon.MachineGun;
            }
        }
        
        private static void SetWeapon(FSWeapon weapon)
        {
            hitScanGun.ResetAnimation();
            hitScanGun.IsUpdateRequested = true;
            switch (weapon)
            {
                case FSWeapon.Uzi:
                default:
                    hitScanGun.WeaponFrames = textureBank[WeaponAnimation.WEAPON01];
                    hitScanGun.HorizontalOffset = -0.3f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = soundBank[WeaponSound.SHOTS5];
                    hitScanGun.EquipSound = soundBank[WeaponSound.UZICOCK3];
                    hitScanGun.ShotConditionCost = 1;
                    hitScanGun.IsBurstFire = true;
                    hitScanGun.IsShotgun = false;
                    break;
                case FSWeapon.M16:
                    hitScanGun.WeaponFrames = textureBank[WeaponAnimation.WEAPON02];
                    hitScanGun.HorizontalOffset = 0.1f;
                    hitScanGun.VerticalOffset = 0.01f;
                    hitScanGun.ShootSound = soundBank[WeaponSound.SHOTS2];
                    hitScanGun.EquipSound = soundBank[WeaponSound.SGCOCK2];
                    hitScanGun.ShotConditionCost = 1;
                    hitScanGun.IsBurstFire = true;
                    hitScanGun.IsShotgun = false;
                    break;
                case FSWeapon.MachineGun:
                    hitScanGun.WeaponFrames = textureBank[WeaponAnimation.WEAPON03];
                    hitScanGun.HorizontalOffset = 0f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = soundBank[WeaponSound.FASTGUN2];
                    hitScanGun.EquipSound = soundBank[WeaponSound.SGCOCK2];
                    hitScanGun.ShotConditionCost = 2;
                    hitScanGun.IsBurstFire = true;
                    hitScanGun.IsShotgun = false;
                    break;
                case FSWeapon.Shotgun:
                    hitScanGun.WeaponFrames = textureBank[WeaponAnimation.WEAPON04];
                    hitScanGun.HorizontalOffset = -0.25f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = soundBank[WeaponSound.SHTGUN];
                    hitScanGun.EquipSound = soundBank[WeaponSound.SGCOCK1];
                    hitScanGun.ShotConditionCost = 20;
                    hitScanGun.IsBurstFire = false;
                    hitScanGun.IsShotgun = true;
                    break;
            }
        }
    }
}
