using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using UnityEngine;

namespace FutureShock
{
    sealed public class HitScanWeapon : MonoBehaviour
    {
        private const float nativeScreenWidth = 320f;
        private const float nativeScreenHeight = 200f;
        private GameObject mainCamera;
        private int playerLayerMask;
        private Rect weaponPosition;
        private int currentFrame;
        private float frameTime;
        private float frameTimeRemaining;
        private float lastScreenWidth, lastScreenHeight;
        public Texture2D[] WeaponFrames { private get; set; }
        public float HorizontalOffset { private get; set; }
        public float VerticalOffset { private get; set; }
        public AudioClip ShootSound { private get; set; }
        public bool IsFiring { get; set; }
        public bool IsBurstFire { private get; set; } // Some weapons fire more than once in an animation cycle
        public int BulletDamage { private get; set; }
        public bool UpdateRequested { private get; set; }

        public void ResetAnimation()
        {
            currentFrame = 0;
            frameTimeRemaining = 0;
            IsFiring = false;
        }

        private void Start()
        {
            frameTime = 0.0625f;
            ResetAnimation();
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
        }

        private void Update()
        {
            if (frameTimeRemaining <= 0f)
            {
                if (IsFiring || currentFrame != 0) // Keep firing until animation is finished.
                {
                    currentFrame = (currentFrame + 1) % WeaponFrames.Length;
                    frameTimeRemaining = frameTime;
                    if (IsBurstFire || currentFrame == 1)
                        FireScanRay();
                    if (currentFrame == 1)
                    {
                        var audioSource = DaggerfallUI.Instance.DaggerfallAudioSource.AudioSource;
                        audioSource.clip = ShootSound;
                        audioSource.volume = DaggerfallUnity.Settings.SoundVolume;
                        audioSource.Play();
                    }
                }
                else
                    return;
            }
            else
                frameTimeRemaining -= Time.deltaTime;
        }

        private void OnGUI()
        {
            // Update weapon when resolution changes
            var screenRect = DaggerfallUI.Instance.CustomScreenRect ?? new Rect(0, 0, Screen.width, Screen.height);
            if (screenRect.width != lastScreenWidth ||
                screenRect.height != lastScreenHeight ||
                UpdateRequested)
            {
                lastScreenWidth = screenRect.width;
                lastScreenHeight = screenRect.height;
                UpdateWeapon();
            }

            if (GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;
            if (Event.current.type.Equals(EventType.Repaint))
                DaggerfallUI.DrawTextureWithTexCoords(weaponPosition, WeaponFrames[currentFrame], new Rect(1, 0, 1 /* -1 to mirror (for left hand) */, 1));
        }

        private void UpdateWeapon()
        {
            var screenRect = DaggerfallUI.Instance.CustomScreenRect ?? new Rect(0, 0, Screen.width, Screen.height);
            var weaponScaleX = (float)screenRect.width / nativeScreenWidth;
            var weaponScaleY = (float)screenRect.height / nativeScreenHeight;
            weaponPosition = new Rect(
                screenRect.x + screenRect.width * (1f + HorizontalOffset) - WeaponFrames[currentFrame].width * weaponScaleX,
                screenRect.y + screenRect.height * (1f + VerticalOffset) - WeaponFrames[currentFrame].height * weaponScaleY,
                WeaponFrames[currentFrame].width * weaponScaleX,
                WeaponFrames[currentFrame].height * weaponScaleY);
        }

        private void FireScanRay()
        {
            const float wepRange = 20f;
            var ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, wepRange, playerLayerMask))
            {
                DealDamage(hit.transform, hit.point, BulletDamage, mainCamera.transform.forward);
            }
        }

        private bool DealDamage(Transform hitTransform, Vector3 impactPosition, int damage, Vector3 direction)
        {
            var entityBehaviour = hitTransform.GetComponent<DaggerfallEntityBehaviour>();
            var mobileUnit = hitTransform.GetComponentInChildren<MobileUnit>();
            var enemyMotor = hitTransform.GetComponent<EnemyMotor>();
            var enemySounds = hitTransform.GetComponent<EnemySounds>();
            var mobileNpc = hitTransform.GetComponent<MobilePersonNPC>();
            var blood = hitTransform.GetComponent<EnemyBlood>();

            // Hit an innocent peasant walking around town.
            if (mobileNpc)
            {
                var playerEntity = GameManager.Instance.PlayerEntity;
                if (!mobileNpc.IsGuard)
                {
                    /*
                    if (blood != null)
                        blood.ShowBloodSplash(0, impactPosition);
                    */
                    mobileNpc.Motor.gameObject.SetActive(false);
                    playerEntity.TallyCrimeGuildRequirements(false, 5);
                    playerEntity.CrimeCommitted = PlayerEntity.Crimes.Murder;
                    playerEntity.SpawnCityGuards(true);
                }
                else
                {
                    playerEntity.CrimeCommitted = PlayerEntity.Crimes.Assault;
                    var guard = playerEntity.SpawnCityGuard(mobileNpc.transform.position, mobileNpc.transform.forward);
                    entityBehaviour = guard.GetComponent<DaggerfallEntityBehaviour>();
                    mobileUnit = guard.GetComponentInChildren<MobileUnit>();
                    enemyMotor = guard.GetComponent<EnemyMotor>();
                    enemySounds = guard.GetComponent<EnemySounds>();
                }

                mobileNpc.Motor.gameObject.SetActive(false);
            }

            if (entityBehaviour == null)
                return false;
            // Hit an enemy.
            if (entityBehaviour.EntityType == EntityTypes.EnemyMonster || entityBehaviour.EntityType == EntityTypes.EnemyClass)
            {
                var enemyEntity = entityBehaviour.Entity as EnemyEntity;
                if (blood != null)
                    blood.ShowBloodSplash(enemyEntity.MobileEnemy.BloodIndex, impactPosition);
                if (enemyMotor != null && enemyMotor.KnockbackSpeed <= (5 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10)) &&
                    entityBehaviour.EntityType == EntityTypes.EnemyClass || enemyEntity.MobileEnemy.Weight > 0)
                {
                    var enemyWeight = (float)enemyEntity.GetWeightInClassicUnits();
                    var tenTimesDamage = damage * 10f;
                    var twoTimesDamage = damage * 2f;
                    var knockBackAmount = ((tenTimesDamage - enemyWeight) * 256f) / (enemyWeight + tenTimesDamage) * twoTimesDamage;
                    var knockBackSpeed = (tenTimesDamage / enemyWeight) * (twoTimesDamage - (knockBackAmount / 256f));
                    knockBackSpeed /= (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10f);
                    if (knockBackSpeed < (15f / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10f)))
                        knockBackSpeed = (15f / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10f));
                    enemyMotor.KnockbackSpeed = knockBackSpeed;
                    enemyMotor.KnockbackDirection = direction;
                }

                if (DaggerfallUnity.Settings.CombatVoices && entityBehaviour.EntityType == EntityTypes.EnemyClass && Dice100.SuccessRoll(40))
                {
                    Genders gender;
                    if (mobileUnit.Enemy.Gender == MobileGender.Male || enemyEntity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                        gender = Genders.Male;
                    else
                        gender = Genders.Female;

                    bool heavyDamage = damage >= enemyEntity.MaxHealth / 4;
                    enemySounds.PlayCombatVoice(gender, false, heavyDamage);
                }

                enemyEntity.DecreaseHealth(damage);
                return true;
            }

            return false;
        }
    }
}
