using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;

namespace Crossbows
{
    public sealed class Crossbows : MonoBehaviour
    {
        public static Crossbows Instance { get; private set; }
        private static Mod mod;
        private const int crossbowTemplateIndex = 289;
        private GameManager gameManager;
        private PlayerEntity playerEntity;
        private PovWeapon povWeapon;
        private DaggerfallUnityItem lastEquippedRight;
        private DaggerfallUnityItem equippedRight;

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
            // Load settings that require a restart.
            var settings = mod.GetSettings();
            var bitcrushed = settings.GetValue<bool>("Options", "DOS-quality sounds");
            // Load sounds.
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

        private void LateUpdate()
        {
            equippedRight = gameManager.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            const int fswItemIndex = 288;
            if (equippedRight == null)
                return;
            var isCustom = IsCustomPovWeapon(equippedRight);
            var noArrows = !playerEntity.Items.Contains(ItemGroups.Weapons, (int)Weapons.Arrow);
            if (!isCustom || noArrows || equippedRight.ConditionPercentage <= 0f || !gameManager.WeaponManager.UsingRightHand) {
                if (!povWeapon.IsHolstered && noArrows)
                    DaggerfallUI.SetMidScreenText(TextManager.Instance.GetLocalizedText("youHaveNoArrows"));
                povWeapon.IsHolstered = true;
                povWeapon.IsFiring = false;
                if (equippedRight.TemplateIndex == fswItemIndex)
                    return;
                goto endCustomLogic;
            }

            if (InputManager.Instance.ActionComplete(InputManager.Actions.SwitchHand) && !gameManager.WeaponManager.enabled) {
                gameManager.WeaponManager.UsingRightHand = false;
                gameManager.WeaponManager.enabled = true;
                DaggerfallUI.Instance.PopupMessage(TextManager.Instance.GetLocalizedText("usingLeftHand"));
                goto endCustomLogic; 
            }

            if (gameManager.WeaponManager.ScreenWeapon.ShowWeapon &&
            gameManager.WeaponManager.EquipCountdownRightHand <= 0f) {
                gameManager.WeaponManager.enabled = false;
                gameManager.WeaponManager.ScreenWeapon.ShowWeapon = false;
                povWeapon.PairedItem = equippedRight;
                povWeapon.WeaponFrames = LoadPovWeaponTexture((WeaponMaterialTypes)equippedRight.NativeMaterialValue);
                povWeapon.PlayEquipSound();
                povWeapon.IsHolstered = gameManager.WeaponManager.Sheathed;
            } 
            else if (!gameManager.WeaponManager.enabled && InputManager.Instance.ActionStarted(InputManager.Actions.ReadyWeapon)) {
                povWeapon.IsHolstered = !povWeapon.IsHolstered;
                gameManager.WeaponManager.Sheathed = povWeapon.IsHolstered;
                if (!povWeapon.IsHolstered)
                    povWeapon.PlayEquipSound();
            }

            povWeapon.IsFiring = !povWeapon.IsHolstered && !gameManager.PlayerEntity.IsParalyzed && gameManager.PlayerEntity.Items.Contains(ItemGroups.Weapons, (int)Weapons.Arrow) && InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon);
            if (!gameManager.WeaponManager.enabled && gameManager.WeaponManager.EquipCountdownRightHand > 0f) {
                povWeapon.IsHolstered = true;
                gameManager.WeaponManager.EquipCountdownRightHand -= 980f * Time.deltaTime;
            }

            endCustomLogic:
            if ((!isCustom && !gameManager.WeaponManager.enabled) || 
            gameManager.WeaponManager.EquipCountdownRightHand > 0f)
                gameManager.WeaponManager.enabled = true;
            lastEquippedRight = equippedRight;
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
