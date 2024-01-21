using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.IO;
using UnityEngine;
using Wenzil.Console;

namespace FutureShock
{
    public sealed class FutureShockWeapons : MonoBehaviour
    {
        private enum WeaponAnimation
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

        private enum ImpactAnimation
        {
            Bullet,
            Laser,
            Grenade,
            RPG,
            Plasma
        }

        private enum ProjectileAnimation
        {
            Grenade
        }

        private enum ProjectileModel
        {
            LASER1,
            LASER2,
        }

        private enum ProjectileTexture
        {
            Laser,
            Plasma,
            Rocket
        }

        private enum WeaponSound
        {
            FASTGUN2,  // Machine Gun
            GRNLAUN2,  // Grenade Launcher
            LASER1,    // Laser Rifle
            LASER2,    // Heavy Laser
            LASER3,    // Heavy Plasma
            //LASER4,  // *pshew* (unused?)
            //LASER5,  // Collection of cartoony laser sounds
            LASER6,    // Plasma Rifle
            //LASER7,  // Plasma Pistol
            //LASER8,  // Plasma Pistol (one of these, not sure which)
            PPCLOAD,   // Energy weapon equip
            ROCKET1,   // Rocket sound
            ROCKET2,   // RPG fire
            SHOTS2,    // M16 (SHOTS3 is seemingly identical)
            SHOTS5,    // Uzi
            SHTGUN,    // Shotgun (includes cocking)
            UZICOCK3,  // Ballistic weapon equip
            EXPLO1,    // Big explosion, meant for grenades I think?
            EXPLO2,    // Small explosion
            EXPLO3     // Big explosion, meant for RPGs?
            //EXPLO4,  // Big explosion, for when big robots die I think.
        }

        private enum FSWeapon
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
        private FutureShockGun fpsGun;
        private uint[] impactAnimMap;
        private uint[] projectileAnimMap;
        //private Dictionary<ProjectileModel, Mesh> projectileMeshBank;
        private Texture2D[][] impactFrameBank;
        private Texture2D[][] projectileFrameBank;
        private readonly Texture2D[] projectileTextures = new Texture2D[3];
        private AudioClip[] weaponSoundBank;
        private DaggerfallUnityItem lastEquippedRight;
        private DaggerfallUnityItem equippedRight;
        private bool ShowWeapon;
        private GameManager gameManager;
        private string gameDataPath;
        private DFPalette shockPalette;
        private const string textureFilePrefix = "TEXTURE.";
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

        private void Start()
        {
            var settings = mod.GetSettings();
            gameDataPath = settings.GetValue<string>("Options", "FutureShock GAMEDATA path");
            gameManager = GameManager.Instance;
            //LoadSettings(settings, new ModSettingsChange());
            if (InitMod())
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
            // When unsheathing, immediately re-sheathe weapon and use HitScanGun in place of FPSWeapon
            var equipChanged = false;
            if (lastEquippedRight != equippedRight)
                equipChanged = true;
            if (IsGun(equippedRight))
            {
                var lastNonGunSheathed = gameManager.WeaponManager.Sheathed;
                if (!lastNonGunSheathed && gameManager.WeaponManager.UsingRightHand)
                    gameManager.WeaponManager.SheathWeapons();
                if (equipChanged)
                {
                    ShowWeapon = (!IsGun(lastEquippedRight) && !lastNonGunSheathed) || !fpsGun.IsHolstered;
                    SetWeapon(GetGunFromMaterial(equippedRight.NativeMaterialValue));
                    fpsGun.PlayEquipSound();
                    fpsGun.IsHolstered = true;
                }
            }
            else if (!fpsGun.IsHolstered)
            {
                fpsGun.IsHolstered = true;
                ShowWeapon = false;
                gameManager.WeaponManager.Sheathed = false;
            }

            if (equipChanged)
                lastEquippedRight = equippedRight;
        }

        // Wait for all other updates to ensure hidden weapon doesn't draw.
        private void LateUpdate()
        {
            equippedRight = gameManager.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            fpsGun.PairedItem = equippedRight;
            if (consoleController.ui.isConsoleOpen || GameManager.IsGamePaused || DaggerfallUI.UIManager.WindowCount != 0)
                return;
            if (equippedRight != null && equippedRight.currentCondition <= 0)
            {
                fpsGun.IsFiring = false;
                ShowWeapon = false;
                fpsGun.IsHolstered = true;
                return;
            }

            // Handle input.
            if (gameManager.WeaponManager.UsingRightHand)
            {
                fpsGun.IsFiring = !fpsGun.IsHolstered && !gameManager.PlayerEntity.IsParalyzed && InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon);
                if (InputManager.Instance.ActionStarted(InputManager.Actions.ReadyWeapon) && IsGun(equippedRight) && !fpsGun.IsFiring)
                    ShowWeapon = !ShowWeapon;
                else if (InputManager.Instance.ActionComplete(InputManager.Actions.SwitchHand) && !fpsGun.IsHolstered)
                    gameManager.WeaponManager.Sheathed = false; // Keep fist "unsheathed" when switching to HTH.
            }
            else if (InputManager.Instance.ActionComplete(InputManager.Actions.SwitchHand) && !gameManager.WeaponManager.Sheathed && IsGun(equippedRight))
                ShowWeapon = true; // Unholster weapon if switching from unsheathed weapon.
            else if (gameManager.WeaponManager.Sheathed && ShowWeapon)
                ShowWeapon = false; // Holster weapon if switched to left hand and sheathed.
            if (!ShowWeapon)
                fpsGun.IsHolstered = true;
            else if (fpsGun.IsHolstered && gameManager.WeaponManager.EquipCountdownRightHand <= 0)
            {
                fpsGun.IsHolstered = false;
                fpsGun.PlayEquipSound();
            }
        }

        private void OnDestroy()
        {
            SaveLoadManager.OnLoad -= SaveLoadManager_OnLoad;
        }

        private static bool IsGun(DaggerfallUnityItem item) => item != null && item.TemplateIndex == ItemFSGun.customTemplateIndex;

        public bool InitMod()
        {
            const string paletteExt = ".COL";
            const string shockPaletteName = "SHOCK" + paletteExt;
            const string dagPaletteName = "ART_PAL" + paletteExt;
            var shockPalettePath = $"{gameDataPath}{shockPaletteName}";
            var dagPalettePath = $"{gameDataPath}{dagPaletteName}";
            // Create copy of Future Shock palette file with file name that TextureFile class uses.
            if (!File.Exists($"{gameDataPath}{dagPaletteName}") && File.Exists(shockPalettePath))
                File.Copy(shockPalettePath, $"{gameDataPath}{dagPaletteName}");
            shockPalette = new DFPalette(shockPalettePath);
            // Map impact/explosion animations (archive, record) to bullet types.
            impactAnimMap = new uint[]
            {
                357 << 16,       // Bullet: 357, 0
                (359 << 16) | 1, // Laser: 359, 1
                360 << 16,       // Grenade: 360, 0
                363 << 16,       // RPG: 363, 0
                364 << 16        // Plasma: 364, 0
            };

            projectileAnimMap = new uint[]
            {
                (512 << 16) | 2 // Grenade: 512, 2
            };

            // Archive 217 has a banned filename. Need to clone it so DFU's texture reader can use it.
            const int badArchive = 217;
            const int goodArchive = 512;
            if (!File.Exists($"{gameDataPath}{textureFilePrefix}{goodArchive}"))
                File.Copy($"{gameDataPath}{textureFilePrefix}{badArchive}", $"{gameDataPath}{textureFilePrefix}{goodArchive}");
            const string bsaExt = ".BSA";
            const string imgsFile = "MDMDIMGS" + bsaExt;
            using (var textureReader = new BsaReader($"{gameDataPath}{imgsFile}"))
            {
                if (textureReader.Reader == null)
                {
                    Debug.Log(GetFileLoadError(imgsFile));
                    return false;
                }

                for (ushort textureIndex = 0; textureIndex < textureReader.IndexCount; textureIndex++)
                {
                    var fileName = textureReader.GetFileName(textureIndex);
                    var fileLength = textureReader.GetFileLength(textureIndex);
                    var cfaPath = $"{gameDataPath}{fileName}";
                    // Skip file if not in bank or already exists.
                    if (!Enum.TryParse(Path.GetFileNameWithoutExtension(fileName), out WeaponAnimation weaponAnimation) || File.Exists(cfaPath))
                    {
                        textureReader.Reader.BaseStream.Seek(fileLength, SeekOrigin.Current);
                        continue;
                    }

                    var textureData = textureReader.Reader.ReadBytes(fileLength);
                    // Create a standalone CFA file that Interkarma's class can use.
                    using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(cfaPath, FileMode.Create)))
                    {
                        binaryWriter.Write(textureData);
                    }
                }
            }
            // Import Sounds
            // Table ripped from Future Shock's memory during runtime
            byte[] cipherTable = { 0xDD, 0x83, 0x65, 0x57, 0xEA, 0x78, 0x08, 0x48, 0xB8, 0x01, 0x38, 0x94, 0x08, 0xDD, 0x3F, 0xC2, 0xBE, 0xAB, 0x76, 0xC6, 0x14 };
            const int soundBankSize = (int)WeaponSound.EXPLO3 + 1;
            weaponSoundBank = new AudioClip[soundBankSize];
            // Look for custom sounds.
            const string directoryPrefix = "FSWeapons_";
            const string soundDirectory = "Sound";
            var soundPath = Path.Combine(Application.streamingAssetsPath, soundDirectory);
            var fsSoundPath = Path.Combine(soundPath, $"{directoryPrefix}{soundDirectory}");
            if (!Directory.Exists(fsSoundPath))
                Directory.CreateDirectory(fsSoundPath);
            const string customSoundExtension = ".ogg";
            for (var i = 0; i <= soundBankSize; i++)
                if (TryLoadSound(fsSoundPath, $"{(WeaponSound)i}{customSoundExtension}", out var audioClip))
                    weaponSoundBank[i] = audioClip;
            // Load sounds from archive.
            const string sfxFile = "MDMDSFXS" + bsaExt;
            using (var soundReader = new BsaReader($"{gameDataPath}{sfxFile}"))
            {
                if (soundReader.Reader == null)
                {
                    Debug.Log(GetFileLoadError(sfxFile));
                    return false;
                }

                const int sampleRate = 11025;
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
                    // Custom file already loaded.
                    if (weaponSoundBank[(int)weaponSound])
                        continue;
                    Decrypt(soundData, cipherTable, 0);
                    var samples = new float[soundData.Length];
                    // Convert each sample byte to float in range -1 to 1.
                    const float conversionFactor = 1.0f / 128.0f;
                    for (var i = 0; i < soundData.Length; i++)
                        samples[i] = (soundData[i] - 128) * conversionFactor;
                    var clip = AudioClip.Create(fileName, fileLength, 1, sampleRate, false);
                    clip.SetData(samples, 0);
                    weaponSoundBank[(int)weaponSound] = clip;
                }
            }

            // Import .3D files
            /*
            projectileMeshBank = new Dictionary<ProjectileModel, Mesh>();
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
                    //var vertexCount = modelData[0] | modelData[1] << 8 | modelData[2] << 16 | modelData[3] << 24;
                    var vertices = new Vector3[39];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        //var dataIndex = 0x60 + i * vertexSize;
                        var dataIndex = i * vertexSize;
                        // Read int32s as little-endian
                        vertices[i].x = modelData[dataIndex    ] | (modelData[dataIndex + 1] << 8) | (modelData[dataIndex + 2 ] << 16) | (modelData[dataIndex + 3 ] << 24);
                        vertices[i].y = modelData[dataIndex + 4] | (modelData[dataIndex + 5] << 8) | (modelData[dataIndex + 6 ] << 16) | (modelData[dataIndex + 7 ] << 24);
                        vertices[i].z = modelData[dataIndex + 8] | (modelData[dataIndex + 9] << 8) | (modelData[dataIndex + 10] << 16) | (modelData[dataIndex + 11] << 24);
                    }

                    var triangles = new int[vertices.Length];
                    for (int i = 0; i < triangles.Length - 2; i += 3)
                    {
                        // I don't know if this is right.
                        triangles[i] = i;
                        triangles[i + 1] = i + 2;
                        triangles[i + 2] = i + 1;
                    }

                    var mesh = new Mesh
                    {
                        vertices = vertices,
                        triangles = triangles
                    };

                    projectileMeshBank[projectileModel] = mesh;
                }
            }

            var meshTest = new GameObject("MeshTest");
            var meshFilter = meshTest.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = projectileMeshBank[ProjectileModel.LASER1];
            */

            // Cache impact/projectile animations.
            impactFrameBank = new Texture2D[(int)ImpactAnimation.Plasma + 1][]
            {
                GetTextureAnimation(ImpactAnimation.Bullet),
                GetTextureAnimation(ImpactAnimation.Laser),
                GetTextureAnimation(ImpactAnimation.Grenade),
                GetTextureAnimation(ImpactAnimation.RPG),
                GetTextureAnimation(ImpactAnimation.Plasma)
            };

            projectileFrameBank = new Texture2D[(int)ProjectileAnimation.Grenade + 1][]
            {
                GetTextureAnimation(ProjectileAnimation.Grenade)
            };

            // Generate textures for projectiles (paints over Daggerfall arrow mesh).
            projectileTextures[(int)ProjectileTexture.Laser] = GetSolidColorTexture(new Color32(255, 0, 0, 255));
            projectileTextures[(int)ProjectileTexture.Plasma] = GetSolidColorTexture(new Color32(0, 255, 255, 255));
            projectileTextures[(int)ProjectileTexture.Rocket] = GetSolidColorTexture(new Color32(255, 255, 255, 255));
            var player = GameObject.FindGameObjectWithTag("Player");
            fpsGun = player.AddComponent<FutureShockGun>();
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(ItemFSGun.customTemplateIndex, ItemGroups.Weapons, typeof(ItemFSGun));
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            return true;
        }

        // Future Shock's data is encrypted by adding a value to each byte from a cipher table.
        // The starting index of the cipher table acts as the key.
        private void Decrypt(byte[] data, byte[] cipher, int key)
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
            if (lastEquippedRight == null)
                return;
            SetWeapon(GetGunFromMaterial(equippedRight.NativeMaterialValue));
        }

        private bool TryLoadSound(string soundPath, string name, out AudioClip audioClip)
        {
            string path = Path.Combine(soundPath, name);
            if (File.Exists(path))
            {
                var www = new WWW("file://" + path); // the "non-deprecated" class gives me compiler errors so it can suck it
                audioClip = www.GetAudioClip(true, true);
                return audioClip != null;
            }

            audioClip = null;
            return false;
        }

        private Texture2D[] GetCfaAnimation(WeaponAnimation textureName) => GetCfaAnimation($"{gameDataPath}{textureName}.CFA", shockPalette);

        private Texture2D[] GetTextureAnimation(ImpactAnimation impactAnimation) => GetTextureAnimation(impactAnimMap, (int)impactAnimation);

        private Texture2D[] GetTextureAnimation(ProjectileAnimation projectileAnimation) => GetTextureAnimation(projectileAnimMap, (int)projectileAnimation);

        private Texture2D[] GetTextureAnimation(uint[] animationMap, int index)
        {
            // High 16 bits = archive, Low 16 bits = record
            var archive = animationMap[index] >> 16;
            var record = 0x0000FFFF & animationMap[index];
            return GetTextureAnimation(gameDataPath, (int)archive, (int)record);
        }

        private static Texture2D[] GetCfaAnimation(string path, DFPalette palette)
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

        private static Texture2D[] GetTextureAnimation(string path, int archiveindex, int recordIndex)
        {
            var textureFile = new TextureFile($"{path}{textureFilePrefix}{archiveindex}", FileUsage.UseMemory, true);
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

            return textures;
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

        private static Texture2D GetSolidColorTexture(Color32 solidColor)
        {
            const int projectileTextureDim = 32;
            var texture = new Texture2D(projectileTextureDim, projectileTextureDim);
            var colors = new Color32[projectileTextureDim * projectileTextureDim];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = solidColor;
            texture.SetPixels32(colors);
            texture.Apply();
            return texture;
        }

        private static string GetFileLoadError(string fileName) => $"Could not load {fileName} from specified path.";
        
        private void SetWeapon(FSWeapon weapon)
        {
            fpsGun.ResetAnimation();
            fpsGun.IsUpdateRequested = true;
            switch (weapon)
            {
                case FSWeapon.Uzi:
                default:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON01);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Bullet];
                    fpsGun.ImpactFrameSize = new Vector2(.5f, .5f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = -.3f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.SHOTS5];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.UZICOCK3];
                    fpsGun.TravelSound = null;
                    fpsGun.ShotConditionCost = 1;
                    fpsGun.SetBurst();
                    fpsGun.ShotSpread = .1f;
                    break;
                case FSWeapon.M16:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON02);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Bullet];
                    fpsGun.ImpactFrameSize = new Vector2(.5f, .5f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = .1f;
                    fpsGun.VerticalOffset = .01f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.SHOTS2];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.UZICOCK3];
                    fpsGun.TravelSound = null;
                    fpsGun.ShotConditionCost = 1;
                    fpsGun.SetBurst();
                    fpsGun.ShotSpread = .03f;
                    break;
                case FSWeapon.MachineGun:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON03);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Bullet];
                    fpsGun.ImpactFrameSize = new Vector2(.5f, .5f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = 0f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.FASTGUN2];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.UZICOCK3];
                    fpsGun.TravelSound = null;
                    fpsGun.ShotConditionCost = 1;
                    fpsGun.SetBurst();
                    fpsGun.ShotSpread = .05f;
                    break;
                case FSWeapon.Shotgun:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON04);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Bullet];
                    fpsGun.ImpactFrameSize = new Vector2(.5f, .5f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = -.25f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.SHTGUN];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.UZICOCK3];
                    fpsGun.TravelSound = null;
                    fpsGun.ShotConditionCost = 3;
                    fpsGun.SetPellets();
                    fpsGun.ShotSpread = .1f;
                    break;
                case FSWeapon.GrenadeLauncher:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON05);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Grenade];
                    fpsGun.ImpactFrameSize = new Vector2(2.5f, 2.5f);
                    fpsGun.ProjectileFrames = projectileFrameBank[(int)ProjectileAnimation.Grenade];
                    fpsGun.ProjectileFrameSize = new Vector2(.2f, .2f);
                    fpsGun.HorizontalOffset = -.1f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.HorizProjAdjust = .12f;
                    fpsGun.VertProjAdjust = .17f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.GRNLAUN2];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.UZICOCK3];
                    fpsGun.TravelSound = null;
                    fpsGun.ImpactSound = weaponSoundBank[(int)WeaponSound.EXPLO1];
                    fpsGun.ShotConditionCost = 50;
                    fpsGun.SetProjectile();
                    fpsGun.IsExplosive = true;
                    fpsGun.IsGrenadeLauncher = true;
                    fpsGun.ProjVelocity = 25f;
                    fpsGun.ProjLightColor = Color.white;
                    fpsGun.ProjPostImpactFade = 2f;
                    break;
                case FSWeapon.RPG:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON06);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.RPG];
                    fpsGun.ImpactFrameSize = new Vector2(3f, 3f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = 0f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.HorizProjAdjust = .17f;
                    fpsGun.VertProjAdjust = .05f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.ROCKET2];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.UZICOCK3];
                    fpsGun.TravelSound = weaponSoundBank[(int)WeaponSound.ROCKET1];
                    fpsGun.ImpactSound = weaponSoundBank[(int)WeaponSound.EXPLO3];
                    fpsGun.IsTravelSoundLooped = false;
                    fpsGun.ShotConditionCost = 100;
                    fpsGun.SetProjectile();
                    fpsGun.ProjectileTexture = projectileTextures[(int)ProjectileTexture.Rocket];
                    fpsGun.IsExplosive = true;
                    fpsGun.IsGrenadeLauncher = false;
                    fpsGun.ProjVelocity = 30f;
                    fpsGun.ProjLightColor = Color.white;
                    fpsGun.ProjPostImpactFade = 1.3f;
                    break;
                case FSWeapon.LaserRifle:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON07);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Laser];
                    fpsGun.ImpactFrameSize = new Vector2(.7f, .7f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = -.05f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.HorizProjAdjust = .12f;
                    fpsGun.VertProjAdjust = .17f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.LASER1];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.PPCLOAD];
                    fpsGun.ImpactSound = weaponSoundBank[(int)WeaponSound.EXPLO2];
                    fpsGun.TravelSound = null;
                    fpsGun.ShotConditionCost = 10;
                    fpsGun.SetProjectileRapid();
                    fpsGun.ProjectileTexture = projectileTextures[(int)ProjectileTexture.Laser];
                    fpsGun.IsExplosive = false;
                    fpsGun.IsGrenadeLauncher = false;
                    fpsGun.ProjVelocity = 45f;
                    fpsGun.ProjLightColor = Color.red;
                    fpsGun.ProjPostImpactFade = 2f;
                    break;
                case FSWeapon.HeavyLaser:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON08);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Laser];
                    fpsGun.ImpactFrameSize = new Vector2(.7f, .7f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = 0f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.HorizProjAdjust = .12f;
                    fpsGun.VertProjAdjust = .17f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.LASER2];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.PPCLOAD];
                    fpsGun.ImpactSound = weaponSoundBank[(int)WeaponSound.EXPLO2];
                    fpsGun.TravelSound = null;
                    fpsGun.ShotConditionCost = 20;
                    fpsGun.SetProjectileRapid();
                    fpsGun.ProjectileTexture = projectileTextures[(int)ProjectileTexture.Laser];
                    fpsGun.IsExplosive = false;
                    fpsGun.IsGrenadeLauncher = false;
                    fpsGun.ProjVelocity = 45f;
                    fpsGun.ProjLightColor = Color.red;
                    fpsGun.ProjPostImpactFade = 2f;
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
                    break;
                */
                case FSWeapon.PlasmaRifle:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON10);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Plasma];
                    fpsGun.ImpactFrameSize = new Vector2(.5f, .5f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = -.1f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.HorizProjAdjust = .13f;
                    fpsGun.VertProjAdjust = .16f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.LASER6];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.PPCLOAD];
                    fpsGun.ImpactSound = weaponSoundBank[(int)WeaponSound.EXPLO2];
                    fpsGun.TravelSound = null;
                    fpsGun.ShotConditionCost = 20;
                    fpsGun.SetProjectile();
                    fpsGun.ProjectileTexture = projectileTextures[(int)ProjectileTexture.Plasma];
                    fpsGun.IsExplosive = false;
                    fpsGun.IsGrenadeLauncher = false;
                    fpsGun.ProjVelocity = 45f;
                    fpsGun.ProjLightColor = Color.cyan;
                    fpsGun.ProjPostImpactFade = 1.7f;
                    break;
                case FSWeapon.HeavyPlasma:
                    fpsGun.WeaponFrames = GetCfaAnimation(WeaponAnimation.WEAPON11);
                    fpsGun.ImpactFrames = impactFrameBank[(int)ImpactAnimation.Plasma];
                    fpsGun.ImpactFrameSize = new Vector2(.5f, .5f);
                    fpsGun.ProjectileFrames = null;
                    fpsGun.HorizontalOffset = 0f;
                    fpsGun.VerticalOffset = 0f;
                    fpsGun.HorizProjAdjust = .12f;
                    fpsGun.VertProjAdjust = .17f;
                    fpsGun.ShootSound = weaponSoundBank[(int)WeaponSound.LASER3];
                    fpsGun.EquipSound = weaponSoundBank[(int)WeaponSound.PPCLOAD];
                    fpsGun.ImpactSound = weaponSoundBank[(int)WeaponSound.EXPLO2];
                    fpsGun.TravelSound = null;
                    fpsGun.ShotConditionCost = 30;
                    fpsGun.SetProjectile();
                    fpsGun.ProjectileTexture = projectileTextures[(int)ProjectileTexture.Plasma];
                    fpsGun.IsExplosive = false;
                    fpsGun.IsGrenadeLauncher = false;
                    fpsGun.ProjVelocity = 45f;
                    fpsGun.ProjLightColor = Color.cyan;
                    fpsGun.ProjPostImpactFade = 1.7f;
                    break;
            }
        }
    }
}
