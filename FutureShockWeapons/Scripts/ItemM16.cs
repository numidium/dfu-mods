using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

namespace FutureShock
{
    sealed public class ItemM16 : DaggerfallUnityItem
    {
        public const int customTemplateIndex = ItemUzi.customTemplateIndex + 1;
        public ItemM16() : base(ItemGroups.Weapons, customTemplateIndex)
        {
        }

        public override int InventoryTextureArchive => 233;
        public override int InventoryTextureRecord => 3;
        public override string ItemName => "M16";
        public override int NativeMaterialValue => (int)WeaponMaterialTypes.Steel;
        public override int GroupIndex { get => Weapons.Longsword - Weapons.Dagger; }
        public override ItemHands GetItemHands() => ItemHands.Both;
        public override EquipSlots GetEquipSlot() => EquipSlots.RightHand;
        public override WeaponTypes GetWeaponType() => WeaponTypes.Bow; // the closest analogue
        public override ItemData_v1 GetSaveData()
        {
            var data = base.GetSaveData();
            data.className = nameof(ItemM16);
            return data;
        }
    }
}
