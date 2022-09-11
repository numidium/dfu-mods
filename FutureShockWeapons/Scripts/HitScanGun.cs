using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using System.Collections;
using UnityEngine;

namespace FutureShock
{
    public class HitScanWeapon : MonoBehaviour
    {
        private const float nativeScreenWidth = 320f;
        private const float nativeScreenHeight = 200f;
        private GameObject mainCamera;
        private int playerLayerMask;
        private Texture2D[] weaponFrames;
        private Rect weaponPosition;
        private int currentFrame;
        public bool IsFiring { get; set; }
        float lastScreenWidth, lastScreenHeight;

        private void Start()
        {
            currentFrame = 0;
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
            var uziCfa = new CfaFile("F:\\dosgames\\futureshock\\doublepack\\Games\\The Terminator - Future Shock\\GAMEDATA\\WEAPON01.CFA", FileUsage.UseMemory, true)
            {
                Palette = new DFPalette("F:\\dosgames\\futureshock\\doublepack\\Games\\The Terminator - Future Shock\\GAMEDATA\\SHOCK.COL")
            };

            var frames = uziCfa.GetFrameCount(0);
            weaponFrames = new Texture2D[frames];
            for (var i = 0; i < frames; i++)
            {
                var bitmap = uziCfa.GetDFBitmap(0, i);
                weaponFrames[i] = new Texture2D(bitmap.Width, bitmap.Height)
                {
                    filterMode = FilterMode.Point
                };

                var colors = uziCfa.GetColor32(0, i, 0);
                weaponFrames[i].SetPixels32(colors);
                weaponFrames[i].Apply();
            }

            StartCoroutine(AnimateWeapon());
        }

        private void OnGUI()
        {
            // Update weapon when resolution changes
            var screenRect = DaggerfallUI.Instance.CustomScreenRect ?? new Rect(0, 0, Screen.width, Screen.height);
            if (screenRect.width != lastScreenWidth ||
                screenRect.height != lastScreenHeight)
                UpdateWeapon();
            if (GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;
            if (Event.current.type.Equals(EventType.Repaint))
                DaggerfallUI.DrawTextureWithTexCoords(weaponPosition, weaponFrames[currentFrame], new Rect(1, 0, 1 /* -1 to mirror (for left hand) */, 1));
        }

        private void UpdateWeapon()
        {
            var screenRect = DaggerfallUI.Instance.CustomScreenRect ?? new Rect(0, 0, Screen.width, Screen.height);
            var weaponScaleX = (float)screenRect.width / nativeScreenWidth;
            var weaponScaleY = (float)screenRect.height / nativeScreenHeight;
            weaponPosition = new Rect(
                screenRect.x + screenRect.width * (1f - 0.3f) - weaponFrames[currentFrame].width * weaponScaleX,
                screenRect.y + screenRect.height - weaponFrames[currentFrame].height * weaponScaleY,
                weaponFrames[currentFrame].width * weaponScaleX,
                weaponFrames[currentFrame].height * weaponScaleY);
        }

        IEnumerator AnimateWeapon()
        {
            while (true)
            {
                var startFrame = currentFrame;
                if (IsFiring)
                    currentFrame = (currentFrame + 1) % weaponFrames.Length;
                else
                    currentFrame = 0;
                if (currentFrame % 2 == 1)
                    FireScanRay();
                if (startFrame != currentFrame)
                    UpdateWeapon();

                yield return new WaitForSeconds(0.0625f); // 1/16th of a second
            }
        }

        private void FireScanRay()
        {
            const float wepRange = 10f;
            const int rayDamage = 10;
            var ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, wepRange, playerLayerMask))
            {
                DealDamage(hit.transform, hit.point, rayDamage, mainCamera.transform.forward);
            }
        }

        private bool DealDamage(Transform hitTransform, Vector3 impactPosition, int damage, Vector3 direction)
        {
            var entityBehaviour = hitTransform.GetComponent<DaggerfallEntityBehaviour>();
            var mobileUnit = hitTransform.GetComponentInChildren<MobileUnit>();
            var enemyMotor = hitTransform.GetComponent<EnemyMotor>();
            var enemySounds = hitTransform.GetComponent<EnemySounds>();
            var mobileNpc = hitTransform.GetComponent<MobilePersonNPC>();
            //var blood = hitTransform.GetComponent<EnemyBlood>();

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
                /*
                if (blood != null)
                    blood.ShowBloodSplash(enemyEntity.MobileEnemy.BloodIndex, impactPosition);
                */
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
