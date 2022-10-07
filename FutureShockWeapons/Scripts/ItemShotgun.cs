using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

namespace FutureShock
{
    sealed public class ItemShotgun : DaggerfallUnityItem
    {
        public const int customTemplateIndex = ItemMachineGun.customTemplateIndex + 1;
        public ItemShotgun() : base(ItemGroups.Weapons, customTemplateIndex)
        {
        }

        public override int InventoryTextureArchive => 233;
        public override int InventoryTextureRecord => 10;
        public override string ItemName => "Shotgun";
        public override int NativeMaterialValue => (int)WeaponMaterialTypes.Steel;
        public override int GroupIndex { get => Weapons.Longsword - Weapons.Dagger; }
        public override ItemHands GetItemHands() => ItemHands.Both;
        public override EquipSlots GetEquipSlot() => EquipSlots.RightHand;
        public override WeaponTypes GetWeaponType() => WeaponTypes.Bow; // the closest analogue
        public override ItemData_v1 GetSaveData()
        {
            var data = base.GetSaveData();
            data.className = nameof(ItemShotgun);
            return data;
        }
    }
}
