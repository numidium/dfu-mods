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
            SetIntArrayValue(data, "Regions", ref model.Regions);
            SetIntValue(data, "FactionId", ref model.FactionId);
            SetIntValue(data, "BuildingType", ref model.BuildingType);
            SetIntValue(data, "SocialGroup", ref model.SocialGroup);
            SetIntValue(data, "QualityMin", ref model.QualityMin);
            SetIntValue(data, "QualityMax", ref model.QualityMax);
            SetIntValue(data, "TextureArchive", ref model.TextureArchive);
            SetIntValue(data, "TextureRecord", ref model.TextureRecord);
            SetIntValue(data, "ReplaceTextureArchive", ref model.ReplaceTextureArchive);
            SetIntValue(data, "ReplaceTextureRecord", ref model.ReplaceTextureRecord);
            SetStringValue(data, "FlatTextureName", ref model.FlatTextureName);
            SetBoolValue(data, "UseExactDimensions", ref model.UseExactDimensions);
            SetIntValue(data, "FlatPortrait", ref model.FlatPortrait);

            return fsResult.Success;
        }

        private void SetIntValue(Dictionary<string, fsData> data, string key, ref int modelValue)
        {
            const int defaultValue = -1;
            if (!data.TryGetValue(key, out var value))
                modelValue = defaultValue;
            else
                modelValue = (int)value.AsInt64;
        }

        private void SetBoolValue(Dictionary<string, fsData> data, string key, ref bool modelValue)
        {
            const bool defaultValue = false;
            if (!data.TryGetValue(key, out var value))
                modelValue = defaultValue;
            else
                modelValue = value.AsBool;
        }

        private void SetStringValue(Dictionary<string, fsData> data, string key, ref string modelValue)
        {
            if (!data.TryGetValue(key, out var value))
                modelValue = null;
            else
                modelValue = value.AsString;
        }

        private void SetIntArrayValue(Dictionary<string, fsData> data, string key, ref int[] modelValue)
        {
            if (!data.TryGetValue(key, out var regions))
            {
                var regionList = regions.AsList;
                modelValue = new int[regionList.Count];
                for (var i = 0; i < regionList.Count; i++)
                    modelValue[i] = (int)regionList[i].AsInt64;
            }
            else
                modelValue = new int[1] { -1 };
        }
    }
}
