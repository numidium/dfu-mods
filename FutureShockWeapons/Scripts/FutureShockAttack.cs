using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility;
using UnityEngine;

namespace FutureShock
{
    public sealed class FutureShockAttack
    {
        public enum ShotResult
        {
            HitTarget,
            MissedTarget,
            HitOther
        }

        public static ShotResult DealDamage(DaggerfallUnityItem weapon, Transform hitTransform, Vector3 impactPosition, Vector3 direction, bool bypassHitSuccessCheck = false)
        {
            // Note: Most of this is adapted from EnemyAttack.cs
            var mobileNpc = hitTransform.GetComponent<MobilePersonNPC>();
            var blood = hitTransform.GetComponent<EnemyBlood>();
            var playerEntity = GameManager.Instance.PlayerEntity;
            DaggerfallEntityBehaviour entityBehaviour;
            MobileUnit mobileUnit;
            EnemyMotor enemyMotor;
            EnemySounds enemySounds;

            // Hit an innocent peasant walking around town.
            if (mobileNpc)
            {
                if (!mobileNpc.IsGuard)
                {
                    if (blood != null)
                        blood.ShowBloodSplash(0, impactPosition);
                    mobileNpc.Motor.gameObject.SetActive(false);
                    playerEntity.TallyCrimeGuildRequirements(false, 5);
                    playerEntity.CrimeCommitted = PlayerEntity.Crimes.Murder;
                    playerEntity.SpawnCityGuards(true);
                    return ShotResult.HitTarget;
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
            else
            {
                entityBehaviour = hitTransform.GetComponent<DaggerfallEntityBehaviour>();
                if (entityBehaviour == null)
                    return ShotResult.HitOther;
                mobileUnit = hitTransform.GetComponentInChildren<MobileUnit>();
                enemyMotor = hitTransform.GetComponent<EnemyMotor>();
                enemySounds = hitTransform.GetComponent<EnemySounds>();
            }

            // Attempt to hit an enemy.
            var isHitSuccessful = false;
            if (entityBehaviour.EntityType == EntityTypes.EnemyMonster || entityBehaviour.EntityType == EntityTypes.EnemyClass)
            {
                var chanceToHitMod = (int)playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Archery);
                chanceToHitMod += FormulaHelper.CalculateWeaponToHit(weapon);
                var proficiencyMods = FormulaHelper.CalculateProficiencyModifiers(playerEntity, weapon);
                var damageModifiers = proficiencyMods.damageMod;
                chanceToHitMod += proficiencyMods.toHitMod;
                var racialMods = FormulaHelper.CalculateRacialModifiers(playerEntity, weapon, playerEntity);
                damageModifiers += racialMods.damageMod;
                chanceToHitMod += racialMods.toHitMod;
                var isEnemyFacingAwayFromPlayer = mobileUnit.IsBackFacing &&
                        mobileUnit.EnemyState != MobileStates.SeducerTransform1 &&
                        mobileUnit.EnemyState != MobileStates.SeducerTransform2;
                var backstabChance = FormulaHelper.CalculateBackstabChance(playerEntity, null, isEnemyFacingAwayFromPlayer);
                chanceToHitMod += backstabChance;
                isHitSuccessful = bypassHitSuccessCheck || FormulaHelper.CalculateSuccessfulHit(playerEntity, entityBehaviour.Entity, chanceToHitMod, FormulaHelper.CalculateStruckBodyPart());
                var damage = FormulaHelper.CalculateWeaponAttackDamage(playerEntity, entityBehaviour.Entity, damageModifiers, 1, weapon);
                if (isHitSuccessful && damage > 0)
                {
                    damage = FormulaHelper.CalculateBackstabDamage(damage, backstabChance);
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
                }

                // Become hostile regardless of hit success.
                if (enemyMotor != null)
                    enemyMotor.MakeEnemyHostileToAttacker(playerEntity.EntityBehaviour);
            }

            return isHitSuccessful ? ShotResult.HitTarget : ShotResult.MissedTarget;
        }

        public static void DamagePlayer(DaggerfallUnityItem weapon)
        {
            var damage = Random.Range(weapon.GetBaseDamageMin(), weapon.GetBaseDamageMax() + 1) + weapon.GetWeaponMaterialModifier();
            GameManager.Instance.PlayerObject.SendMessage("RemoveHealth", damage);
        }
    }
}
