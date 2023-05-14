using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace Crossbows
{
    // Credits: Interkarma and Allofich for their work on DaggerfallMissile.cs, which this class is based on.
    [RequireComponent(typeof(DaggerfallAudioSource))]
    public sealed class CustomMissile : MonoBehaviour
    {
        private readonly float collisionRadius = 0.35f;
        private readonly float lifespanInSeconds = 8f;
        private Vector3 direction;
        private Vector3 collisionPosition;
        private GameObject lightGo;
        private DaggerfallAudioSource daggerfallAudioSource;
        private float lifespan = 0f;
        private bool missileReleased = false;
        private bool impactDetected = false;
        private bool impactAssigned = false;
        private GameObject goProjectile = null;
        private int playerLayerMask;
        private bool isWaitTick = false;
        public float Velocity { private get; set; }
        public float PostImpactFade { get; set; }
        public DaggerfallUnityItem OriginWeapon { private get; set; }
        public SoundClips ImpactSound { private get; set; }
        public float HorizontalAdjust { private get; set; }
        public float VerticalAdjust { private get; set; }
        public DaggerfallEntityBehaviour Caster { get; set; }
        public bool IsArrow { get; set; }
        public bool IsSummoned { get; set; }

        private void Awake()
        {
            daggerfallAudioSource = transform.GetComponent<DaggerfallAudioSource>();
        }

        private void Start()
        {
            // Setup light and shadows
            lightGo = new GameObject("ProjectileLight");
            lightGo.transform.parent = transform;

            // Setup projectile
            Vector3 adjust;
            // Adjust to fit gun HUD position.
            adjust = (GameManager.Instance.MainCamera.transform.rotation * -Caster.transform.up) * VerticalAdjust;
            goProjectile = GameObjectHelper.CreateDaggerfallMeshGameObject(99800, transform, ignoreCollider: true); // TODO: Use proper models
            if (!GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal)
                adjust += GameManager.Instance.MainCamera.transform.right * HorizontalAdjust;
            else
                adjust -= GameManager.Instance.MainCamera.transform.right * HorizontalAdjust;
            goProjectile.transform.localPosition = adjust;
            goProjectile.transform.rotation = Quaternion.LookRotation(GameManager.Instance.MainCamera.transform.forward);
            goProjectile.layer = gameObject.layer;
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
        }

        private void Update()
        {
            if (!missileReleased)
            {
                direction = GameManager.Instance.MainCamera.transform.forward;
                transform.position = GameManager.Instance.MainCamera.transform.position;
                collisionPosition = transform.position;
                missileReleased = true;
            }

            var frameDeltaTime = Time.deltaTime;
            if (!impactDetected)
            {
                // Transform missile along direction vector
                transform.position += (direction * Velocity) * frameDeltaTime;
                if (lifespan > lifespanInSeconds)
                    Destroy(gameObject);
            }
            else
            {
                if (!impactAssigned)
                {
                    transform.position = collisionPosition; // Match visual position with collider position.
                    daggerfallAudioSource.PlayOneShot(ImpactSound);
                    impactAssigned = true;
                }
                else
                {
                    // Wait for audio clip.
                    if (!daggerfallAudioSource.IsPlaying())
                        Destroy(gameObject);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!missileReleased || impactDetected)
                return;
            lifespan += Time.fixedDeltaTime;
            var tickTime = Time.fixedDeltaTime * 2f; // Half the global physics tick rate.
            if (!isWaitTick)
            {
                var displacement = (direction * Velocity) * tickTime;
                if (Physics.Raycast(collisionPosition, direction, out var hitInfo, displacement.magnitude + collisionRadius, playerLayerMask))
                {
                    // Place self at meeting point with collider and self-destruct.
                    collisionPosition = hitInfo.point - direction * collisionRadius;
                    HandleCollision(hitInfo.collider);
                    return;
                }
                else
                    collisionPosition += displacement;
            }

            // Only process every other physics tick.
            isWaitTick = !isWaitTick;
        }

        private void HandleCollision(Collider other)
        {
            if (impactDetected)
                return;
            // Get entity based on collision type
            DaggerfallEntityBehaviour entityBehaviour;
            if (other != null)
                entityBehaviour = other.gameObject.transform.GetComponent<DaggerfallEntityBehaviour>();
            else
                return;
            if (other != null)
            {
                if (entityBehaviour)
                    DamageTarget(other);
            }

            Destroy(goProjectile);
            impactDetected = true;
        }

        private void DamageTarget(Collider arrowHitCollider)
        {
            // Assumes caster is player for now.
            Transform hitTransform = arrowHitCollider.gameObject.transform;
            if (OriginWeapon != null)
                DealDamage(OriginWeapon, IsArrow, IsSummoned, hitTransform, hitTransform.position, goProjectile.transform.forward);
        }

        // Adapted from WeaponManager.cs
        // Returns true if hit an enemy entity
        private bool DealDamage(DaggerfallUnityItem strikingWeapon, bool arrowHit, bool arrowSummoned, Transform hitTransform, Vector3 impactPosition, Vector3 direction)
        {
            var playerEntity = GameManager.Instance.PlayerEntity;
            var entityBehaviour = hitTransform.GetComponent<DaggerfallEntityBehaviour>();
            var entityMobileUnit = hitTransform.GetComponentInChildren<MobileUnit>();
            var enemyMotor = hitTransform.GetComponent<EnemyMotor>();
            var enemySounds = hitTransform.GetComponent<EnemySounds>();

            // Check if hit a mobile NPC
            var mobileNpc = hitTransform.GetComponent<MobilePersonNPC>();
            if (mobileNpc)
            {
                if (!mobileNpc.IsGuard)
                {
                    var blood = hitTransform.GetComponent<EnemyBlood>();
                    if (blood)
                    {
                        blood.ShowBloodSplash(0, impactPosition);
                    }

                    mobileNpc.Motor.gameObject.SetActive(false);
                    playerEntity.TallyCrimeGuildRequirements(false, 5);
                    playerEntity.CrimeCommitted = PlayerEntity.Crimes.Murder;
                    playerEntity.SpawnCityGuards(true);

                    // Allow custom race handling of weapon hit against mobile NPCs, e.g. vampire feeding or lycanthrope killing
                    if (entityBehaviour)
                    {
                        entityBehaviour.Entity.SetHealth(0);
                        var racialOverride = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();
                        racialOverride?.OnWeaponHitEntity(GameManager.Instance.PlayerEntity, entityBehaviour.Entity);
                    }
                }
                else
                {
                    playerEntity.CrimeCommitted = PlayerEntity.Crimes.Assault;
                    var guard = playerEntity.SpawnCityGuard(mobileNpc.transform.position, mobileNpc.transform.forward);
                    entityBehaviour = guard.GetComponent<DaggerfallEntityBehaviour>();
                    entityMobileUnit = guard.GetComponentInChildren<MobileUnit>();
                    enemyMotor = guard.GetComponent<EnemyMotor>();
                    enemySounds = guard.GetComponent<EnemySounds>();
                }

                mobileNpc.Motor.gameObject.SetActive(false);
            }

            // Check if hit an entity and remove health
            if (entityBehaviour)
            {
                if (entityBehaviour.EntityType == EntityTypes.EnemyMonster || entityBehaviour.EntityType == EntityTypes.EnemyClass)
                {
                    var enemyEntity = entityBehaviour.Entity as EnemyEntity;

                    // Calculate damage
                    int animTime = (int)(GameManager.Instance.WeaponManager.ScreenWeapon.GetAnimTime() * 1000); // Get animation time, converted to ms.
                    bool isEnemyFacingAwayFromPlayer = entityMobileUnit.IsBackFacing &&
                        entityMobileUnit.EnemyState != MobileStates.SeducerTransform1 &&
                        entityMobileUnit.EnemyState != MobileStates.SeducerTransform2;
                    int damage = FormulaHelper.CalculateAttackDamage(playerEntity, enemyEntity, isEnemyFacingAwayFromPlayer, animTime, strikingWeapon);

                    // Break any "normal power" concealment effects on player
                    if (playerEntity.IsMagicallyConcealedNormalPower && damage > 0)
                        EntityEffectManager.BreakNormalPowerConcealmentEffects(GameManager.Instance.PlayerEntityBehaviour);

                    // Add arrow to target's inventory
                    if (arrowHit && !arrowSummoned)
                    {
                        DaggerfallUnityItem arrow = ItemBuilder.CreateWeapon(Weapons.Arrow, WeaponMaterialTypes.None);
                        arrow.stackCount = 1;
                        enemyEntity.Items.AddItem(arrow);
                    }

                    // Play hit sound and trigger blood splash at hit point
                    if (damage > 0)
                    {
                        if (GameManager.Instance.WeaponManager.UsingRightHand)
                            enemySounds.PlayHitSound(GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand));
                        else
                            enemySounds.PlayHitSound(GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand));

                        var blood = hitTransform.GetComponent<EnemyBlood>();
                        if (blood)
                        {
                            blood.ShowBloodSplash(enemyEntity.MobileEnemy.BloodIndex, impactPosition);
                        }

                        // Knock back enemy based on damage and enemy weight
                        if (enemyMotor)
                        {
                            if (enemyMotor.KnockbackSpeed <= (5 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10)) &&
                                entityBehaviour.EntityType == EntityTypes.EnemyClass ||
                                enemyEntity.MobileEnemy.Weight > 0)
                            {
                                float enemyWeight = enemyEntity.GetWeightInClassicUnits();
                                float tenTimesDamage = damage * 10;
                                float twoTimesDamage = damage * 2;

                                float knockBackAmount = ((tenTimesDamage - enemyWeight) * 256) / (enemyWeight + tenTimesDamage) * twoTimesDamage;
                                float KnockbackSpeed = (tenTimesDamage / enemyWeight) * (twoTimesDamage - (knockBackAmount / 256));
                                KnockbackSpeed /= (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10);

                                if (KnockbackSpeed < (15 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10)))
                                    KnockbackSpeed = (15 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10));
                                enemyMotor.KnockbackSpeed = KnockbackSpeed;
                                enemyMotor.KnockbackDirection = direction;
                            }
                        }

                        if (DaggerfallUnity.Settings.CombatVoices && entityBehaviour.EntityType == EntityTypes.EnemyClass && Dice100.SuccessRoll(40))
                        {
                            Genders gender;
                            if (entityMobileUnit.Enemy.Gender == MobileGender.Male || enemyEntity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                                gender = Genders.Male;
                            else
                                gender = Genders.Female;

                            bool heavyDamage = damage >= enemyEntity.MaxHealth / 4;
                            enemySounds.PlayCombatVoice(gender, false, heavyDamage);
                        }
                    }
                    else
                    {
                        enemySounds.PlayParrySound();
                    }

                    // Handle weapon striking enchantments - this could change damage amount
                    if (strikingWeapon != null && strikingWeapon.IsEnchanted)
                    {
                        EntityEffectManager effectManager = GetComponent<EntityEffectManager>();
                        if (effectManager)
                            damage = effectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Strikes, strikingWeapon, GameManager.Instance.PlayerEntity.Items, enemyEntity.EntityBehaviour, damage);
                        strikingWeapon.RaiseOnWeaponStrikeEvent(entityBehaviour, damage);
                    }

                    // Remove health
                    enemyEntity.DecreaseHealth(damage);

                    // Handle attack from player
                    enemyEntity.EntityBehaviour.HandleAttackFromSource(GameManager.Instance.PlayerEntityBehaviour);

                    // Allow custom race handling of weapon hit against enemies, e.g. vampire feeding or lycanthrope killing
                    var racialOverride = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();
                    racialOverride?.OnWeaponHitEntity(GameManager.Instance.PlayerEntity, entityBehaviour.Entity);

                    // Skill tally
                    playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);

                    return true;
                }
            }

            return false;
        }
    }
}
