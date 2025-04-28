using FullSerializer;

namespace FlatReplacer
{
    [fsObject(Converter = typeof(ReplacementConverter))]
    public class FlatReplacementRecord
    {
        public int[] Regions;
        public int FactionId;
        public int SocialGroup;
        public int BuildingType;
        public int QualityMin;
        public int QualityMax;
        public int TextureArchive;
        public int TextureRecord;
        public int ReplaceTextureArchive;
        public int ReplaceTextureRecord;
        public string FlatTextureName;
        public bool UseExactDimensions;
        public int FlatPortrait;
    }
}
