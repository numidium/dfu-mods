using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;

namespace Crossbows
{
    public sealed class ItemCrossbow : DaggerfallUnityItem
    {
        public const int customTemplateIndex = 289;
        private const string itemName = "Crossbow";
        public override int InventoryTextureArchive => GameManager.Instance.PlayerEntity.Gender == DaggerfallWorkshop.Game.Entity.Genders.Female ? 1803 : 1802;
        public override int InventoryTextureRecord => 1;
        public override int GetBaseDamageMin() => 11;
        public override int GetBaseDamageMax() => 25;
        public override string ItemName => IsIdentified ? shortName.Replace("%it", itemName) : itemName;
        public override string LongName => $"{DaggerfallUnity.Instance.TextProvider.GetWeaponMaterialName((WeaponMaterialTypes)NativeMaterialValue)} {ItemName}";
        public override int GroupIndex => 0;
        public override ItemHands GetItemHands() => ItemHands.Both;
        public override WeaponTypes GetWeaponType() => WeaponTypes.Bow;
        public override int GetWeaponSkillUsed() => (int)DaggerfallConnect.DFCareer.ProficiencyFlags.MissileWeapons;
        public ItemCrossbow() : base(ItemGroups.Weapons, customTemplateIndex) { }
        public override ItemData_v1 GetSaveData()
        {
            var data = base.GetSaveData();
            data.className = $"Crossbows.{nameof(ItemCrossbow)}";
            return data;
        }
    }
}
