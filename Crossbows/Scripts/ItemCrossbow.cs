using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossbows
{
    public sealed class ItemCrossbow : DaggerfallUnityItem
    {
        public const int customTemplateIndex = 289;
        private const string itemName = "Crossbow";
        // Vanilla weapon textures (placeholder)
        public override int InventoryTextureArchive => GameManager.Instance.PlayerEntity.Gender == DaggerfallWorkshop.Game.Entity.Genders.Female ? 234 : 233;
        // Long Bow (placeholder)
        public override int InventoryTextureRecord => 11;
        // +10 from longbows to compensate for slower reload.
        public override int GetBaseDamageMin() => 14;
        public override int GetBaseDamageMax() => 28;
        public override string ItemName => itemName;
        public override string LongName => $"{DaggerfallUnity.Instance.TextProvider.GetWeaponMaterialName((WeaponMaterialTypes)NativeMaterialValue)} {itemName}";
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
