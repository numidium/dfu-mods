using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

namespace FutureShock
{
    public sealed class ItemFSGun : DaggerfallUnityItem
    {
        public const int customTemplateIndex = 288;
        public ItemFSGun() : base(ItemGroups.Weapons, customTemplateIndex)
        {
        }

        public override int InventoryTextureArchive => 233;
        public override int InventoryTextureRecord => 1;
        public override int GetBaseDamageMin() => 1;
        public override int GetBaseDamageMax() => 2;
        public override string ItemName => GunName;
        public override string LongName => GunName;
        public override int GroupIndex => 0;
        public override ItemHands GetItemHands() => ItemHands.Both;
        public override WeaponTypes GetWeaponType() => WeaponTypes.Staff; // Just need something that is 2-handed
        public override ItemData_v1 GetSaveData()
        {
            var data = base.GetSaveData();
            data.className = $"FutureShock.{nameof(ItemFSGun)}";
            return data;
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
