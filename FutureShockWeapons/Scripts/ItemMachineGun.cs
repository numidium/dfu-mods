using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

namespace FutureShock
{
    sealed public class ItemMachineGun : DaggerfallUnityItem
    {
        public const int customTemplateIndex = ItemM16.customTemplateIndex + 1;
        public ItemMachineGun() : base(ItemGroups.Weapons, customTemplateIndex)
        {
        }

        public override int InventoryTextureArchive => 233;
        public override int InventoryTextureRecord => 7;
        public override string ItemName => "Machine Gun";
        public override int NativeMaterialValue => (int)WeaponMaterialTypes.Steel;
        public override int GroupIndex { get => Weapons.Battle_Axe - Weapons.Dagger; }
        public override ItemHands GetItemHands() => ItemHands.Both;
        public override EquipSlots GetEquipSlot() => EquipSlots.RightHand;
        public override WeaponTypes GetWeaponType() => WeaponTypes.Bow; // the closest analogue
        public override ItemData_v1 GetSaveData()
        {
            var data = base.GetSaveData();
            data.className = nameof(ItemMachineGun);
            return data;
        }
    }
}
