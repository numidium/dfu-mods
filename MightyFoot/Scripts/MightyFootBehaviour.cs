using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using UnityEngine;
using Wenzil.Console;

namespace MightyFoot
{
    public class MightyFootBehaviour : MonoBehaviour
    {
        public FPSWeapon Kicker { get; set; }
        public string BindText { get; set; }
        public bool ShowMessage { get; set; }
        private GameObject mainCamera;
        private int playerLayerMask;
        private WeaponManager weaponManager;
        private KeyCode kickKey = KeyCode.K;
        private const float messageCooldownTime = 5.0f;
        private float timeSinceLastKick;
        private bool isDamageFinished;
        private ConsoleController consoleController;

        void Start()
        {
            weaponManager = GameManager.Instance.WeaponManager;
            var player = GameManager.Instance.PlayerObject;
            Kicker = player.AddComponent<FPSWeapon>();
            Kicker.WeaponType = WeaponTypes.Melee;
            Kicker.ShowWeapon = false;
            Kicker.FlipHorizontal = weaponManager.ScreenWeapon.FlipHorizontal;
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            SetKickKeyFromText(BindText);
            timeSinceLastKick = messageCooldownTime;
            isDamageFinished = false;
            consoleController = GameObject.Find("Console").GetComponent<ConsoleController>();
        }

        void OnGUI()
        {
            // Needs to be executed here to prevent the player's fist from drawing.
            Kicker.ShowWeapon = !(Kicker.WeaponState == WeaponStates.Idle);
        }

        void Update()
        {
            // Prevent kicking while in menus. Thanks, |3lessed.
            if (consoleController.ui.isConsoleOpen || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress || DaggerfallUI.UIManager.WindowCount != 0)
                return;

            // Perform forward kick on keypress and hide weapon from HUD when attack finishes.
            var playerEntity = GameManager.Instance.PlayerEntity;
            var weaponManager = GameManager.Instance.WeaponManager;
            if (InputManager.Instance.GetKey(kickKey) &&
                !Kicker.IsAttacking() &&
                !weaponManager.ScreenWeapon.IsAttacking() &&
                weaponManager.ScreenWeapon.WeaponType != WeaponTypes.Melee &&
                weaponManager.ScreenWeapon.WeaponType != WeaponTypes.Werecreature)
            {
                Kicker.ShowWeapon = true;
                Kicker.OnAttackDirection(WeaponManager.MouseDirections.Up);
                if (ShowMessage && timeSinceLastKick >= messageCooldownTime)
                    DaggerfallUI.AddHUDText("Mighty foot engaged");
                timeSinceLastKick = 0.0f;
            }
            else
                timeSinceLastKick += Time.deltaTime;

            // Damage the target on hit.
            if (isDamageFinished && Kicker.GetCurrentFrame() == 0)
                isDamageFinished = false;
            if (!isDamageFinished && Kicker.GetCurrentFrame() == Kicker.GetHitFrame())
            {
                bool hitEnemy;
                MeleeDamage(Kicker, out hitEnemy);
                if (!hitEnemy)
                    Kicker.PlaySwingSound();
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
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            RaycastHit hit;
            if (Physics.SphereCast(ray, 0.25f, out hit, weapon.Reach, playerLayerMask))
            {
                if (!GameManager.Instance.WeaponManager.WeaponEnvDamage(null, hit)
                   || Physics.Raycast(ray, out hit, weapon.Reach, playerLayerMask))
                    hitEnemy = weaponManager.WeaponDamage(null, false, false, hit.transform, hit.point, mainCamera.transform.forward);
            }
        }

        private void SetKickKeyFromText(string text)
        {
            KeyCode result;
            if (System.Enum.TryParse(text, out result))
                kickKey = result;
            else
                kickKey = KeyCode.K;
        }
    }
}
