using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

namespace FutureShock
{
    public sealed class ItemFSGun : DaggerfallUnityItem
    {
        private const string templateName = "Firearm"; // Make sure this is synced with ItemTemplates.json
        public const int customTemplateIndex = 288;
        public override int InventoryTextureArchive => GameManager.Instance.PlayerEntity.Gender == DaggerfallWorkshop.Game.Entity.Genders.Female ? 1801 : 1800;
        public override int InventoryTextureRecord => 1;
        public override int GetBaseDamageMin() => 2 + PlasmaBonus + ExplosiveBonus;
        public override int GetBaseDamageMax() => 3 + PlasmaBonus + ExplosiveBonus;
        public override string ItemName => shortName == templateName || !IsIdentified ? GunName : shortName.Replace("%it", GunName);
        public override string LongName => ItemName;
        public override int GroupIndex => 0;
        public override ItemHands GetItemHands() => ItemHands.Both;
        public override WeaponTypes GetWeaponType() => WeaponTypes.Staff; // Just need something that is 2-handed
        public override int GetWeaponSkillUsed() => (int)DaggerfallConnect.DFCareer.ProficiencyFlags.MissileWeapons;
        public ItemFSGun() : base(ItemGroups.Weapons, customTemplateIndex)
        {
        }

        public override ItemData_v1 GetSaveData()
        {
            var data = base.GetSaveData();
            data.className = $"FutureShock.{nameof(ItemFSGun)}";
            return data;
        }

        // Assumes that explosive weapons are Orcish or above.
        private int ExplosiveBonus => NativeMaterialValue >= (int)WeaponMaterialTypes.Orcish ? 7 : 0;
        // Plasma weapons fire much slower than lasers so they need a leg up.
        private int PlasmaBonus {
            get
            {
                if (NativeMaterialValue == (int)WeaponMaterialTypes.Adamantium)
                    return 8;
                else if (NativeMaterialValue == (int)WeaponMaterialTypes.Ebony)
                    return 10;
                else
                    return 0;
            }
        }

        private string GunName
        {
            get
            { 
                switch (NativeMaterialValue)
                {
                    case (int)WeaponMaterialTypes.Iron:
                        return "Uzi";
                    case (int)WeaponMaterialTypes.Steel:
                        return "M16";
                    case (int)WeaponMaterialTypes.Silver:
                        return "Shotgun";
                    case (int)WeaponMaterialTypes.Elven:
                        return "Machine Gun";
                    case (int)WeaponMaterialTypes.Dwarven:
                        return "Laser Rifle";
                    case (int)WeaponMaterialTypes.Mithril:
                        return "Heavy Laser";
                    case (int)WeaponMaterialTypes.Adamantium:
                        return "Plasma Rifle";
                    case (int)WeaponMaterialTypes.Ebony:
                        return "Heavy Plasma";
                    case (int)WeaponMaterialTypes.Orcish:
                        return "Grenade Launcher";
                    case (int)WeaponMaterialTypes.Daedric:
                        return "RPG";
                    default:
                        return "Unknown Gun";
                }
            }
        }
    }
}
