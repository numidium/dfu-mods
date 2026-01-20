using FullSerializer;

namespace DynamicAmbience
{
    [fsObject(Converter = typeof(AmbienceConverter))]
    public sealed class AmbienceRecord
    {
        public string Name;
        public bool? Night;
        public bool? Interior;
        public bool? Dungeon;
        public bool? DungeonCastle;
        public int[] LocationType;
        public int[] BuildingType;
        public int[] WeatherType;
        public int[] FactionId;
        public int[] ClimateIndex;
        public int[] RegionIndex;
        public int[] DungeonType;
        public int[] BuildingQuality;
        public int[] Season;
        public int[] Month;
        public bool? StartMenu;
        public bool? ReadingBook;
        public bool? Combat;
        public bool? Swimming;
        public bool? Positional;
        public bool? BuildingIsOpen;
        public int? MinDelay;
        public int? MaxDelay;
        public int[] FlatRecord;
    }
}