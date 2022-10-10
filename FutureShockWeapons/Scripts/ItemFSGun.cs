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
        public override int GetBaseDamageMax() => 3;
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
                    case (int)WeaponMaterialTypes.Steel:
                    case (int)WeaponMaterialTypes.Silver:
                        return "Uzi";
                    case (int)WeaponMaterialTypes.Elven:
                    case (int)WeaponMaterialTypes.Dwarven:
                        return "M16";
                    case (int)WeaponMaterialTypes.Mithril:
                    case (int)WeaponMaterialTypes.Adamantium:
                        return "Shotgun";
                    default:
                        return "Machine Gun";
                }
            }
        }
    }
}
