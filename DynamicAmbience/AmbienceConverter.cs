using FullSerializer;
using System;
using System.Collections.Generic;

namespace DynamicAmbience
{
    public sealed class AmbienceConverter : fsDirectConverter<AmbienceRecord>
    {
        public override object CreateInstance(fsData data, Type storageType)
        {
            return new AmbienceRecord();
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref AmbienceRecord model)
        {
            SetStringValue(data, "Name", out model.Name);
            SetBoolValue(data, "Night", out model.Night);
            SetBoolValue(data, "Interior", out model.Interior);
            SetBoolValue(data, "Dungeon", out model.Dungeon);
            SetBoolValue(data, "DungeonCastle", out model.DungeonCastle);
            SetIntArrayValue(data, "LocationType", out model.LocationType);
            SetIntArrayValue(data, "BuildingType", out model.BuildingType);
            SetIntArrayValue(data, "WeatherType", out model.WeatherType);
            SetIntArrayValue(data, "FactionId", out model.FactionId);
            SetIntArrayValue(data, "ClimateIndex", out model.ClimateIndex);
            SetIntArrayValue(data, "RegionIndex", out model.RegionIndex);
            SetIntArrayValue(data, "DungeonType", out model.DungeonType);
            SetIntArrayValue(data, "BuildingQuality", out model.BuildingQuality);
            SetIntArrayValue(data, "Season", out model.Season);
            SetIntArrayValue(data, "Month", out model.Month);
            SetBoolValue(data, "StartMenu", out model.StartMenu);
            SetBoolValue(data, "Combat", out model.Combat);
            SetBoolValue(data, "Swimming", out model.Swimming);
            SetBoolValue(data, "Submerged", out model.Submerged);
            SetBoolValue(data, "Positional", out model.Positional);
            SetBoolValue(data, "BuildingIsOpen", out model.BuildingIsOpen);
            SetIntValue(data, "MinDelay", out model.MinDelay);
            SetIntValue(data, "MaxDelay", out model.MaxDelay);

            return fsResult.Success;
        }

        protected override fsResult DoSerialize(AmbienceRecord model, Dictionary<string, fsData> serialized)
        {
            return fsResult.Success;
        }

        private void SetIntValue(Dictionary<string, fsData> data, string key, out int? modelValue)
        {
            if (!data.ContainsKey(key) || !data[key].IsInt64 || !data.TryGetValue(key, out var value))
                modelValue = null;
            else
                modelValue = (int?)value.AsInt64;
        }

        private void SetBoolValue(Dictionary<string, fsData> data, string key, out bool? modelValue)
        {
            if (!data.ContainsKey(key) || !data.TryGetValue(key, out var value))
                modelValue = null;
            else
                modelValue = value.AsBool;
        }

        private void SetStringValue(Dictionary<string, fsData> data, string key, out string modelValue)
        {
            if (!data.ContainsKey(key) || !data[key].IsString || !data.TryGetValue(key, out var value))
                modelValue = null;
            else
                modelValue = value.AsString;
        }

        private void SetIntArrayValue(Dictionary<string, fsData> data, string key, out int[] modelValue)
        {
            if (!data.ContainsKey(key) || !data.TryGetValue(key, out var regions))
            {
                modelValue = null;
            }
            else
            {
                var regionList = regions.AsList;
                modelValue = new int[regionList.Count];
                for (var i = 0; i < regionList.Count; i++)
                    modelValue[i] = (int)regionList[i].AsInt64;
            }
        }
    }
}