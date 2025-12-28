using FullSerializer;
using System;
using System.Collections.Generic;

namespace FlatReplacer
{
    public class ReplacementConverter : fsDirectConverter<FlatReplacementRecord>
    {
        public override object CreateInstance(fsData data, Type storageType)
        {
            return new FlatReplacementRecord();
        }

        protected override fsResult DoSerialize(FlatReplacementRecord model, Dictionary<string, fsData> serialized)
        {
            // Serialization shouldn't be done from inside DFU in normal circumstances.
            return fsResult.Success;
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref FlatReplacementRecord model)
        {
            SetIntArrayValue(data, "Regions", out model.Regions);
            SetIntValue(data, "LocationTypes", out model.LocationTypes, 0);
            SetIntValue(data, "FactionId", out model.FactionId);
            SetIntValue(data, "BuildingType", out model.BuildingType);
            SetIntValue(data, "SocialGroup", out model.SocialGroup);
            SetIntValue(data, "NameBank", out model.NameBank);
            SetIntValue(data, "QualityMin", out model.QualityMin, 1);
            SetIntValue(data, "QualityMax", out model.QualityMax, 20);
            SetIntValue(data, "TextureArchive", out model.TextureArchive);
            SetIntValue(data, "TextureRecord", out model.TextureRecord);
            SetIntValue(data, "ReplaceTextureArchive", out model.ReplaceTextureArchive);
            SetIntValue(data, "ReplaceTextureRecord", out model.ReplaceTextureRecord);
            SetStringValue(data, "FlatTextureName", out model.FlatTextureName);
            SetBoolValue(data, "UseExactDimensions", out model.UseExactDimensions);
            SetIntValue(data, "FlatPortrait", out model.FlatPortrait);
            SetIntValue(data, "Priority", out model.Priority, 0);

            return fsResult.Success;
        }

        private void SetIntValue(Dictionary<string, fsData> data, string key, out int modelValue, int defaultValue = -1)
        {
            if (!data.ContainsKey(key) || !data[key].IsInt64 || !data.TryGetValue(key, out var value))
                modelValue = defaultValue;
            else
                modelValue = (int)value.AsInt64;
        }

        private void SetBoolValue(Dictionary<string, fsData> data, string key, out bool modelValue)
        {
            const bool defaultValue = false;
            if (!data.ContainsKey(key) || !data.TryGetValue(key, out var value))
                modelValue = defaultValue;
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
                modelValue = new int[1] { -1 };
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
