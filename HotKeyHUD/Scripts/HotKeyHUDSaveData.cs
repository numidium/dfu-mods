using DaggerfallWorkshop.Game.MagicAndEffects;
using System.Collections.Generic;

namespace HotKeyHUD
{
    public enum PayloadType { None, Item, Spell };

    [FullSerializer.fsObject("v1")]
    public sealed class HotKeyHUDSaveData
    {
        // If the payload is either an Item or a Spell then save to/load from the next position in their respective lists.
        public List<bool> forceUseSlots;
        public List<PayloadType> payloadTypes;
        public List<ulong> itemUids;
        public List<EffectBundleSettings> spells;
    }
}
