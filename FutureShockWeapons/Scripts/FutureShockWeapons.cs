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
            WEAPON01,     // Uzi
            WEAPON02,     // M16
            WEAPON03,     // Machine Gun
            WEAPON04,     // Shotgun
            WEAPON05,     // Grenade Launcher
            WEAPON06,     // RPG
            WEAPON07,     // Laser Rifle
            WEAPON08,     // Heavy Laser
            //WEAPON09,   // Plasma Pistol
            WEAPON10,     // Plasma Rifle
            WEAPON11      // Heavy Plasma
            //WEAPON12    // Red-tinted Uzi (?)
            // Unusable files (removed?)
            /*
            WEAPON13,
            WEAPON14,
            WEAPON15,
            WEAPON16,
            WEAPON17,
            WEAPON18,
            WEAPON19,
            WEAPON20,
            WEAPON21,
            WEAPON22,
            WEAPON23,
            WEAPON24
            */
        }

        enum ImpactAnimation
        {
            Bullet,
            Laser,
            Grenade,
            RPG,
            Plasma
        }

        enum ProjectileModel
        {
            LASER1,
            LASER2,
        }

        enum WeaponSound
        {
            FASTGUN2,  // Machine Gun
            GRNLAUN2,  // Grenade Launcher
            LASER1,    // Laser Rifle
            LASER2,    // Heavy Laser
            LASER3,    // Heavy Plasma
            //LASER4,    // *pshew* (unused?)
            //LASER5,    // Collection of cartoony laser sounds
            LASER6,    // Plasma Rifle
            //LASER7,    // Plasma Pistol
            //LASER8,    // Plasma Pistol (one of these, not sure which)
            PPCLOAD,   // Energy weapon equip
            ROCKET1,   // Rocket sound
            ROCKET2,   // RPG fire
            SHOTS2,    // M16 (SHOTS3 is seemingly identical)
            SHOTS5,    // Uzi
            SHTGUN,    // Shotgun (includes cocking)
            UZICOCK3   // Ballistic weapon equip
        }

        enum FSWeapon
        {
            Uzi,
            M16,
            MachineGun,
            Shotgun,
            GrenadeLauncher,
            RPG,
            LaserRifle,
            HeavyLaser,
            PlasmaPistol,
            PlasmaRifle,
            HeavyPlasma
        }

        private static Mod mod;
        private static ConsoleController consoleController;
        private FutureShockGun hitScanGun;
        private Dictionary<WeaponAnimation, Texture2D[]> weaponAnimBank;
        private Dictionary<ImpactAnimation, Texture2D[]> impactAnimBank;
        private Dictionary<ProjectileModel, Mesh> projectileMeshBank;
        private Dictionary<WeaponSound, AudioClip> weaponSoundBank;
        private DaggerfallUnityItem lastEquippedRight;
        private DaggerfallUnityItem equippedRight;
        private bool ShowWeapon;
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

        private void OnDestroy()
        {
            SaveLoadManager.OnLoad -= SaveLoadManager_OnLoad;
        }

        private static bool IsGun(DaggerfallUnityItem item) => item != null && item.TemplateIndex == ItemFSGun.customTemplateIndex;

        public bool InitMod(string gameDataPath)
        {
            var shockPalette = new DFPalette($"{gameDataPath}SHOCK.COL");
            // Import HUD animations
            weaponAnimBank = new Dictionary<WeaponAnimation, Texture2D[]>();
            // Check for and/or load loose CFA files. Normally these will not exist until first run.
            foreach (WeaponAnimation textureName in Enum.GetValues(typeof(WeaponAnimation)))
                weaponAnimBank[textureName] = GetTextureAnimFromCfaFile($"{gameDataPath}{textureName}.CFA", shockPalette);
            // Import impact/explosion animations
            impactAnimBank = new Dictionary<ImpactAnimation, Texture2D[]>();
            PopulateImpactAnimsFromFile($"{gameDataPath}TEXTURE.357", impactAnimBank, 0, ImpactAnimation.Bullet);
            PopulateImpactAnimsFromFile($"{gameDataPath}TEXTURE.358", impactAnimBank, 0, ImpactAnimation.RPG);
            PopulateImpactAnimsFromFile($"{gameDataPath}TEXTURE.359", impactAnimBank, 1, ImpactAnimation.Laser);
            PopulateImpactAnimsFromFile($"{gameDataPath}TEXTURE.360", impactAnimBank, 0, ImpactAnimation.Grenade);
            PopulateImpactAnimsFromFile($"{gameDataPath}TEXTURE.364", impactAnimBank, 0, ImpactAnimation.Plasma);
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
                    if (!Enum.TryParse(Path.GetFileNameWithoutExtension(fileName), out WeaponAnimation weaponAnimation) || weaponAnimBank[weaponAnimation] != null)
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

                    weaponAnimBank[weaponAnimation] = GetTextureAnimFromCfaFile(cfaPath, shockPalette);
                }
            }
            // Import Sounds
            // Table ripped from Future Shock's memory during runtime
            byte[] cipherTable = { 0xDD, 0x83, 0x65, 0x57, 0xEA, 0x78, 0x08, 0x48, 0xB8, 0x01, 0x38, 0x94, 0x08, 0xDD, 0x3F, 0xC2, 0xBE, 0xAB, 0x76, 0xC6, 0x14 };
            weaponSoundBank = new Dictionary<WeaponSound, AudioClip>();
            using (var soundReader = new BsaReader($"{gameDataPath}MDMDSFXS.BSA"))
            {
                if (soundReader.Reader == null)
                {
                    Debug.Log("Could not load MDMDSFXS.BSA from specified path.");
                    return false;
                }

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
                    Decrypt(soundData, cipherTable, 0);
                    var samples = new float[soundData.Length];
                    // Convert each sample byte to float in range -1 to 1.
                    const float conversionFactor = 1.0f / 128.0f;
                    for (var i = 0; i < soundData.Length; i++)
                        samples[i] = (soundData[i] - 128) * conversionFactor;
                    var clip = AudioClip.Create(fileName, fileLength, 1, 11025, false);
                    clip.SetData(samples, 0);
                    weaponSoundBank[weaponSound] = clip;
                }
            }

            // Import .3D files
            projectileMeshBank = new Dictionary<ProjectileModel, Mesh>();
            /*
            using (var modelReader = new BsaReader($"{gameDataPath}MDMDENMS.BSA"))
            {
                if (modelReader == null)
                {
                    Debug.Log("Could not load MDMDENMS.BSA from specified path.");
                    return false;
                }

                for (ushort modelIndex = 0; modelIndex < modelReader.IndexCount; modelIndex++)
                {
                    var fileName = modelReader.GetFileName(modelIndex);
                    var fileLength = modelReader.GetFileLength(modelIndex);
                    // Skip file if not in bank.
                    if (!Enum.TryParse(Path.GetFileNameWithoutExtension(fileName), out ProjectileModel projectileModel))
                    {
                        modelReader.Reader.BaseStream.Seek(fileLength, SeekOrigin.Current);
                        continue;
                    }

                    var modelData = modelReader.Reader.ReadBytes(fileLength);
                    var key = 0;
                    switch (projectileModel)
                    {
                        case ProjectileModel.LASER1:
                            key = 19;
                            break;
                        case ProjectileModel.LASER2:
                            key = 4;
                            break;
                        default:
                            key = 0;
                            break;
                    }

                    Decrypt(modelData, cipherTable, key);
                    const int vertexSize = 12;
                    var vertexCount = modelData[0] | modelData[1] << 8 | modelData[2] << 16 | modelData[3] << 24;
                    var vertices = new Vector3[vertexCount];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var dataIndex = 0x60 + i * vertexSize;
                        // Read int32s as little-endian
                        vertices[i].x = modelData[dataIndex    ] | (modelData[dataIndex + 1] << 8) | (modelData[dataIndex + 2 ] << 16) | (modelData[dataIndex + 3 ] << 24);
                        vertices[i].y = modelData[dataIndex + 4] | (modelData[dataIndex + 5] << 8) | (modelData[dataIndex + 6 ] << 16) | (modelData[dataIndex + 7 ] << 24);
                        vertices[i].z = modelData[dataIndex + 8] | (modelData[dataIndex + 9] << 8) | (modelData[dataIndex + 10] << 16) | (modelData[dataIndex + 11] << 24);
                    }

                    var triangles = new int[vertices.Length - 2];
                    for (int i = 0; i < triangles.Length - 2; i++)
                        triangles[i] = i; // I don't know if this is right.

                    var mesh = new Mesh
                    {
                        vertices = vertices,
                        triangles = triangles
                    };

                    projectileMeshBank[projectileModel] = mesh;
                }
            }
            */

            /*
            var meshTest = new GameObject("MeshTest");
            var meshFilter = meshTest.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = projectileMeshBank[ProjectileModel.LASER1];
            */

            var player = GameObject.FindGameObjectWithTag("Player");
            hitScanGun = player.AddComponent<FutureShockGun>();
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemFSGun.customTemplateIndex, ItemGroups.Weapons, typeof(ItemFSGun));
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            return true;
        }

        void Decrypt(byte[] data, byte[] cipher, int key)
        {
            var cipherIndex = key;
            for (var i = 0; i < data.Length; i++)
            {
                data[i] -= cipher[cipherIndex];
                cipherIndex = (cipherIndex + 1) % cipher.Length;
            }
        }

        private void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            lastEquippedRight = equippedRight = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            SetWeapon(GetGunFromMaterial(equippedRight.NativeMaterialValue));
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

        private static void PopulateImpactAnimsFromFile(string path, Dictionary<ImpactAnimation, Texture2D[]> bank, int recordIndex, ImpactAnimation bankIndex)
        {
            var textureFile = new TextureFile(path, FileUsage.UseMemory, true);
            var frameCount = textureFile.GetFrameCount(recordIndex);
            var textures = new Texture2D[frameCount];
            for (int frame = 0; frame < frameCount; frame++)
            {
                var colors = textureFile.GetColor32(recordIndex, frame, 0);
                textures[frame] = new Texture2D(textureFile.GetWidth(recordIndex), textureFile.GetHeight(recordIndex))
                {
                    filterMode = FilterMode.Point
                };

                textures[frame].SetPixels32(colors);
                textures[frame].Apply();
            }

            bank[bankIndex] = textures;
        }

        private static FSWeapon GetGunFromMaterial(int material)
        {
            switch ((WeaponMaterialTypes)material)
            {
                case WeaponMaterialTypes.Iron:
                    return FSWeapon.Uzi;
                case WeaponMaterialTypes.Steel:
                    return FSWeapon.M16;
                case WeaponMaterialTypes.Silver:
                    return FSWeapon.Shotgun;
                case WeaponMaterialTypes.Elven:
                    return FSWeapon.MachineGun;
                case WeaponMaterialTypes.Dwarven:
                    return FSWeapon.LaserRifle;
                case WeaponMaterialTypes.Mithril:
                    return FSWeapon.HeavyLaser;
                case WeaponMaterialTypes.Adamantium:
                    return FSWeapon.PlasmaRifle;
                case WeaponMaterialTypes.Ebony:
                    return FSWeapon.HeavyPlasma;
                case WeaponMaterialTypes.Orcish:
                    return FSWeapon.GrenadeLauncher;
                default:
                    return FSWeapon.RPG;
            }
        }
        
        private void SetWeapon(FSWeapon weapon)
        {
            hitScanGun.ResetAnimation();
            hitScanGun.IsUpdateRequested = true;
            switch (weapon)
            {
                case FSWeapon.Uzi:
                default:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON01];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Bullet];
                    hitScanGun.HorizontalOffset = -.3f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.SHOTS5];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.UZICOCK3];
                    hitScanGun.ShotConditionCost = 1;
                    hitScanGun.SetBurst();
                    hitScanGun.ShotSpread = .15f;
                    break;
                case FSWeapon.M16:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON02];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Bullet];
                    hitScanGun.HorizontalOffset = .1f;
                    hitScanGun.VerticalOffset = .01f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.SHOTS2];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.UZICOCK3];
                    hitScanGun.ShotConditionCost = 1;
                    hitScanGun.SetBurst();
                    hitScanGun.ShotSpread = .05f;
                    break;
                case FSWeapon.MachineGun:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON03];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Bullet];
                    hitScanGun.HorizontalOffset = 0f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.FASTGUN2];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.UZICOCK3];
                    hitScanGun.ShotConditionCost = 2;
                    hitScanGun.SetBurst();
                    hitScanGun.ShotSpread = .1f;
                    break;
                case FSWeapon.Shotgun:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON04];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Bullet];
                    hitScanGun.HorizontalOffset = -.25f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.SHTGUN];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.UZICOCK3];
                    hitScanGun.ShotConditionCost = 20;
                    hitScanGun.SetPellets();
                    hitScanGun.ShotSpread = .2f;
                    break;
                case FSWeapon.GrenadeLauncher:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON05];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Grenade];
                    hitScanGun.HorizontalOffset = -.1f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.GRNLAUN2];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.UZICOCK3];
                    hitScanGun.ShotConditionCost = 50;
                    hitScanGun.SetProjectile();
                    hitScanGun.ShotSpread = .2f;
                    break;
                case FSWeapon.RPG:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON06];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.RPG];
                    hitScanGun.HorizontalOffset = 0f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.ROCKET2];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.UZICOCK3];
                    hitScanGun.ShotConditionCost = 100;
                    hitScanGun.SetProjectile();
                    hitScanGun.ShotSpread = .2f;
                    break;
                case FSWeapon.LaserRifle:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON07];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Laser];
                    hitScanGun.HorizontalOffset = -.05f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.LASER1];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.PPCLOAD];
                    hitScanGun.ShotConditionCost = 10;
                    hitScanGun.SetProjectile();
                    hitScanGun.ShotSpread = .2f;
                    break;
                case FSWeapon.HeavyLaser:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON08];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Laser];
                    hitScanGun.HorizontalOffset = 0f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.LASER2];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.PPCLOAD];
                    hitScanGun.ShotConditionCost = 20;
                    hitScanGun.SetProjectile();
                    hitScanGun.ShotSpread = .2f;
                    break;
                /*
                case FSWeapon.PlasmaPistol:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON09];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Impact1];
                    hitScanGun.HorizontalOffset = -.15f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.LASER7];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.PPCLOAD];
                    hitScanGun.ShotConditionCost = 20;
                    hitScanGun.IsBurstFire = false;
                    hitScanGun.IsShotgun = false;
                    hitScanGun.ShotSpread = .2f;
                    break;
                */
                case FSWeapon.PlasmaRifle:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON10];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Plasma];
                    hitScanGun.HorizontalOffset = -.1f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.LASER6];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.PPCLOAD];
                    hitScanGun.ShotConditionCost = 20;
                    hitScanGun.SetProjectile();
                    hitScanGun.ShotSpread = .2f;
                    break;
                case FSWeapon.HeavyPlasma:
                    hitScanGun.WeaponFrames = weaponAnimBank[WeaponAnimation.WEAPON11];
                    hitScanGun.ImpactFrames = impactAnimBank[ImpactAnimation.Plasma];
                    hitScanGun.HorizontalOffset = 0f;
                    hitScanGun.VerticalOffset = 0f;
                    hitScanGun.ShootSound = weaponSoundBank[WeaponSound.LASER3];
                    hitScanGun.EquipSound = weaponSoundBank[WeaponSound.PPCLOAD];
                    hitScanGun.ShotConditionCost = 30;
                    hitScanGun.SetProjectile();
                    hitScanGun.ShotSpread = .2f;
                    break;
            }
        }
    }
}
