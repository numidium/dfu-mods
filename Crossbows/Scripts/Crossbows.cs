using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.Items;
using Wenzil.Console;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;

namespace Crossbows
{
    public sealed class Crossbows : MonoBehaviour
    {
        public static Crossbows Instance { get; private set; }
        private static Mod mod;
        private const int crossbowTemplateIndex = 289;
        private ConsoleController consoleController;
        private GameManager gameManager;
        private PlayerEntity playerEntity;
        private PovWeapon povWeapon;
        private DaggerfallUnityItem lastEquippedRight;
        private DaggerfallUnityItem equippedRight;
        private bool ShowWeapon;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<Crossbows>();
            Instance.povWeapon = go.AddComponent<PovWeapon>();
            Instance.povWeapon.HorizontalOffset = .003125f; // 1/320
            //mod.LoadSettingsCallback = Instance.LoadSettings;
        }

        private void Start()
        {
            // Load sounds.
            var bitcrushed = true;
            var soundPrefix = "crossbow";
            var soundPostfix = bitcrushed ? "_lo" : string.Empty;
            var soundExtension = ".ogg";
            ModManager.Instance.TryGetAsset($"{soundPrefix}_equip{soundPostfix}{soundExtension}", false, out AudioClip equipSound);
            equipSound.LoadAudioData();
            ModManager.Instance.TryGetAsset($"{soundPrefix}_load{soundPostfix}{soundExtension}", false, out AudioClip loadSound);
            loadSound.LoadAudioData();
            ModManager.Instance.TryGetAsset($"{soundPrefix}_ready{soundPostfix}{soundExtension}", false, out AudioClip readySound);
            readySound.LoadAudioData();
            ModManager.Instance.TryGetAsset($"{soundPrefix}_fire{soundPostfix}{soundExtension}", false, out AudioClip shootSound);
            shootSound.LoadAudioData();
            consoleController = GameObject.Find("Console").GetComponent<ConsoleController>();
            gameManager = GameManager.Instance;
            playerEntity = gameManager.PlayerEntity;
            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(crossbowTemplateIndex, ItemGroups.Weapons, typeof(ItemCrossbow));
            // Initialize POV weapon.
            povWeapon.LaunchFrame = 3;
            povWeapon.EquipSound = equipSound;
            povWeapon.LoadSound = loadSound;
            povWeapon.ReadySound = readySound;
            povWeapon.ShootSound = shootSound;
            povWeapon.IsHolstered = false;
            povWeapon.ShotConditionCost = 3;
            povWeapon.CooldownTimeMultiplier = 2.5f;
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            Debug.Log($"{mod.Title} initialized.");
            mod.IsReady = true;
        }

        private void Update()
        {
            // When unsheathing, immediately re-sheathe weapon and use PovWeapon in place of FPSWeapon
            var equipChanged = false;
            if (lastEquippedRight != equippedRight)
                equipChanged = true;
            if (IsCustomPovWeapon(equippedRight))
            {
                var lastNonCustomSheathed = gameManager.WeaponManager.Sheathed;
                if (!lastNonCustomSheathed)
                    gameManager.WeaponManager.SheathWeapons();
                if (equipChanged)
                {
                    ShowWeapon = (!IsCustomPovWeapon(lastEquippedRight) && !lastNonCustomSheathed) || !povWeapon.IsHolstered;
                    povWeapon.PlayEquipSound();
                    povWeapon.IsHolstered = true;
                    povWeapon.WeaponFrames = LoadPovWeaponTexture((WeaponMaterialTypes)equippedRight.NativeMaterialValue);
                }
            }
            else if (!povWeapon.IsHolstered)
            {
                povWeapon.IsHolstered = true;
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
            povWeapon.PairedItem = equippedRight;
            if (consoleController.ui.isConsoleOpen || GameManager.IsGamePaused || DaggerfallUI.UIManager.WindowCount != 0)
                return;
            if (equippedRight != null && (equippedRight.currentCondition <= 0 || playerEntity.Items.GetItem(ItemGroups.Weapons, (int)Weapons.Arrow, allowQuestItem: false) == null))
            {
                povWeapon.IsFiring = false;
                ShowWeapon = false;
                if (!povWeapon.IsHolstered)
                    DaggerfallUI.SetMidScreenText(TextManager.Instance.GetLocalizedText("youHaveNoArrows"));
                povWeapon.IsHolstered = true;
                return;
            }

            povWeapon.IsFiring = !povWeapon.IsHolstered && !playerEntity.IsParalyzed && InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon);
            if (InputManager.Instance.ActionStarted(InputManager.Actions.ReadyWeapon) && IsCustomPovWeapon(equippedRight) && !povWeapon.IsFiring)
                ShowWeapon = !ShowWeapon;
            if (!ShowWeapon)
                povWeapon.IsHolstered = true;
            else if (povWeapon.IsHolstered && gameManager.WeaponManager.EquipCountdownRightHand <= 0)
            {
                povWeapon.IsHolstered = false;
                povWeapon.PlayEquipSound();
            }
        }

        private void OnDestroy()
        {
            SaveLoadManager.OnLoad -= SaveLoadManager_OnLoad;
        }

        private void LogAssetLoadError(string assetName) => Debug.Log($"{mod.Title} failed to initialize. Could not load asset: {assetName}");
        private static bool IsCustomPovWeapon(DaggerfallUnityItem item) => item != null && item.TemplateIndex == ItemCrossbow.customTemplateIndex;

        private void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            lastEquippedRight = equippedRight = playerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            if (lastEquippedRight == null)
                return;
            if (IsCustomPovWeapon(equippedRight))
                povWeapon.WeaponFrames = LoadPovWeaponTexture((WeaponMaterialTypes)equippedRight.NativeMaterialValue);
            // Will need to put code to set weapon attributes here if multiple weapon types are defined.
        }

        /// <summary>
        /// Loads POV textures from mod assets.
        /// </summary>
        /// <param name="weaponMaterial">The weapon material of the textures to be loaded.</param>
        /// <returns>An array of textures.</returns>
        private Texture2D[] LoadPovWeaponTexture(WeaponMaterialTypes weaponMaterial)
        {
            const int animationFrames = 6;
            var povTextures = new Texture2D[animationFrames];
            for (var i = 0; i < povTextures.Length; i++)
            {
                var textureName = $"CROSSBOW_{weaponMaterial}_{i}.png";
                if (TextureReplacement.TryImportImage(textureName, true, out var texture))
                {
                    texture.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
                    povTextures[i] = texture;
                }
                else
                    LogAssetLoadError(textureName);
            }

            return povTextures;
        }
    }
}
