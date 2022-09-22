using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FutureShock
{
    sealed public class HitScanWeapon : MonoBehaviour
    {
        private const float nativeScreenWidth = 320f;
        private const float nativeScreenHeight = 200f;
        private GameObject mainCamera;
        private int playerLayerMask;
        private Texture2D[] weaponFrames;
        private Rect weaponPosition;
        private int currentFrame;
        private float frameTime;
        private float frameTimeRemaining;
        private float lastScreenWidth, lastScreenHeight;
        private AudioClip shootSound;
        private static readonly byte[] noiseTable = { 0xDD, 0x83, 0x65, 0x57, 0xEA, 0x78, 0x08, 0x48, 0xB8, 0x01, 0x38, 0x94, 0x08, 0xDD, 0x3F, 0xC2, 0xBE, 0xAB, 0x76, 0xC6, 0x14 };
        public bool IsFiring { get; set; }

        private void Start()
        {
            currentFrame = 0;
            frameTime = 0.0625f;
            frameTimeRemaining = 0f;
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

            var soundData = File.ReadAllBytes("F:\\dosgames\\futureshock\\doublepack\\Games\\The Terminator - Future Shock\\GAMEDATA\\SHOTS2.RAW");
            DeNoisify(ref soundData);
            var samples = new float[soundData.Length];
            const float divisor = 1.0f / 128.0f;
            for (var i = 0; i < soundData.Length; i++)
                samples[i] = (soundData[i] - 128) * divisor;
            shootSound = AudioClip.Create("SHOTS2", soundData.Length, 1, 11025, false);
            shootSound.SetData(samples, 0);
        }

        private void DeNoisify(ref byte[] samples)
        {
            var tableInd = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] -= noiseTable[tableInd];
                tableInd = (tableInd + 1) % noiseTable.Length;
            }
        }

        private void Update()
        {
            if (frameTimeRemaining <= 0f)
            {
                if (IsFiring || currentFrame != 0) // Keep firing until animation is finished.
                {
                    currentFrame = (currentFrame + 1) % weaponFrames.Length;
                    frameTimeRemaining = frameTime;
                    FireScanRay();
                    if (currentFrame == 1)
                    {
                        var audioSource = DaggerfallUI.Instance.DaggerfallAudioSource.AudioSource;
                        audioSource.clip = shootSound;
                        audioSource.volume = DaggerfallUnity.Settings.SoundVolume;
                        if (!audioSource.isPlaying)
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
                screenRect.height != lastScreenHeight)
            {
                lastScreenWidth = screenRect.width;
                lastScreenHeight = screenRect.height;
                UpdateWeapon();
            }

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
