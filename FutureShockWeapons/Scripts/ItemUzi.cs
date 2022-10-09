using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

namespace FutureShock
{
    public sealed class ItemUzi : DaggerfallUnityItem
    {
        public const int customTemplateIndex = 288; // basis for all other indices used in this mod
        public ItemUzi() : base(ItemGroups.Weapons, customTemplateIndex)
        {
        }

        public override int InventoryTextureArchive => 233;
        public override int InventoryTextureRecord => 1;
        public override string ItemName => "Uzi";
        public override int NativeMaterialValue => (int)WeaponMaterialTypes.Steel;
        public override int GroupIndex { get => Weapons.Dagger - Weapons.Dagger; }
        public override ItemHands GetItemHands() => DaggerfallUnity.Settings.BowLeftHandWithSwitching ? ItemHands.LeftOnly : ItemHands.Both;
        public override WeaponTypes GetWeaponType() => WeaponTypes.Bow; // the closest analogue
        public override ItemData_v1 GetSaveData()
        {
            var data = base.GetSaveData();
            data.className = $"FutureShock.{nameof(ItemUzi)}";
            return data;
        }
    }
}
