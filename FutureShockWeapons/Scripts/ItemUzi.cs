using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

namespace FutureShock
{
    sealed public class ItemUzi : DaggerfallUnityItem
    {
        public const int customTemplateIndex = 288;
        public ItemUzi() : base(ItemGroups.Weapons, customTemplateIndex)
        {
        }

        public override int InventoryTextureArchive => 233;
        public override string ItemName => "Uzi";
        public override string LongName => "Uzi 9mm";
        public override int NativeMaterialValue => (int)WeaponMaterialTypes.Steel;
        public override int GroupIndex { get => 5; }
        public override ItemHands GetItemHands() => ItemHands.Both;
        public override EquipSlots GetEquipSlot() => EquipSlots.RightHand;
        public override WeaponTypes GetWeaponType() => WeaponTypes.Bow; // the closest analogue
        public override ItemData_v1 GetSaveData()
        {
            var data = base.GetSaveData();
            data.className = nameof(ItemUzi);
            return data;
        }
    }
}
