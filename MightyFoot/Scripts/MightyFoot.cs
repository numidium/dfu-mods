using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using Wenzil.Console;
using DaggerfallWorkshop;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Serialization;

namespace MightyFoot
{
    public sealed class MightyFoot : MonoBehaviour
    {
        private static Mod mod;
        private static MightyFoot instance;
        private static string bindText;
        private static bool isMessageEnabled;
        private FPSWeapon kicker;
        private GameObject mainCamera;
        private int playerLayerMask;
        private WeaponManager weaponManager;
        private const KeyCode defaultKickKey = KeyCode.K;
        private KeyCode kickKey = defaultKickKey;
        private const float messageCooldownTime = 5.0f;
        private float timeSinceLastKick;
        private bool isDamageFinished;
        private ConsoleController consoleController;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            instance = go.AddComponent<MightyFoot>();
            mod.LoadSettingsCallback = instance.LoadSettings;
        }

        private void Awake()
        {
            var settings = mod.GetSettings();
            LoadSettings(settings, new ModSettingsChange());
            Debug.Log("Mighty Foot initialized.");
            mod.IsReady = true;
        }

        // Load settings that can change during runtime.
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            bindText = settings.GetValue<string>("Options", "Keybind");
            SetKickKeyFromText(bindText);
            isMessageEnabled = settings.GetValue<bool>("Options", "Display HUD Text");
        }

        private void Start()
        {
            weaponManager = GameManager.Instance.WeaponManager;
            var player = GameManager.Instance.PlayerObject;
            kicker = player.AddComponent<FPSWeapon>();
            kicker.WeaponType = WeaponTypes.Melee;
            kicker.ShowWeapon = false;
            kicker.FlipHorizontal = weaponManager.ScreenWeapon.FlipHorizontal;
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            SetKickKeyFromText(bindText);
            timeSinceLastKick = messageCooldownTime;
            isDamageFinished = false;
            consoleController = GameObject.Find("Console").GetComponent<ConsoleController>();
        }

        private void OnGUI()
        {
            // Needs to be executed here to prevent the player's fist from drawing.
            kicker.ShowWeapon = !(kicker.WeaponState == WeaponStates.Idle);
        }

        private void Update()
        {
            // Prevent kicking while in menus. Thanks, |3lessed.
            if (consoleController.ui.isConsoleOpen || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress || DaggerfallUI.UIManager.WindowCount != 0)
                return;
            // Perform forward kick on keypress and hide weapon from HUD when attack finishes.
            var gameManager = GameManager.Instance;
            var playerEntity = gameManager.PlayerEntity;
            var weaponManager = gameManager.WeaponManager;
            if (InputManager.Instance.GetKey(kickKey) &&
                !kicker.IsAttacking() &&
                !weaponManager.ScreenWeapon.IsAttacking() &&
                weaponManager.ScreenWeapon.WeaponType != WeaponTypes.Melee &&
                weaponManager.ScreenWeapon.WeaponType != WeaponTypes.Werecreature &&
                gameManager.TransportManager.TransportMode != TransportModes.Horse &&
                gameManager.TransportManager.TransportMode != TransportModes.Cart)
            {
                kicker.ShowWeapon = true;
                kicker.OnAttackDirection(WeaponManager.MouseDirections.Up);
                if (isMessageEnabled && timeSinceLastKick >= messageCooldownTime)
                    DaggerfallUI.AddHUDText("Mighty foot engaged");
                timeSinceLastKick = 0.0f;
            }
            else
                timeSinceLastKick += Time.deltaTime;

            // Damage the target on hit.
            if (isDamageFinished && kicker.GetCurrentFrame() == 0)
                isDamageFinished = false;
            if (!isDamageFinished && kicker.GetCurrentFrame() == kicker.GetHitFrame())
            {
                MeleeDamage(kicker, out bool hitEnemy);
                if (!hitEnemy)
                    kicker.PlaySwingSound();
                isDamageFinished = true;
                if (hitEnemy)
                {
                    // Advance skills
                    playerEntity.TallySkill(DFCareer.Skills.HandToHand, 1);
                    playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
                }
            }
        }

        // The following method is a pared down version of WeaponManager.MeleeDamage as of version 0.10.27
        private void MeleeDamage(FPSWeapon weapon, out bool hitEnemy)
        {
            hitEnemy = false;
            if (!mainCamera || !weapon)
                return;
            // Fire ray along player facing using weapon range
            var ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.SphereCast(ray, 0.25f, out RaycastHit hit, weapon.Reach, playerLayerMask))
            {
                if (!GameManager.Instance.WeaponManager.WeaponEnvDamage(null, hit)
                   || Physics.Raycast(ray, out hit, weapon.Reach, playerLayerMask))
                    hitEnemy = weaponManager.WeaponDamage(null, false, false, hit.transform, hit.point, mainCamera.transform.forward);
            }
        }

        private void SetKickKeyFromText(string text)
        {
            if (System.Enum.TryParse(text, out KeyCode result))
                kickKey = result;
            else
            {
                kickKey = defaultKickKey;
                Debug.Log("Mighty Foot: Detected an invalid key code. Setting to default.");
            }
        }
    }
}
