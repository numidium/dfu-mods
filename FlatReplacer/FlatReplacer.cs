using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility.AssetInjection;
using FullSerializer;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FlatReplacer
{
    public sealed class FlatReplacer : MonoBehaviour
    {
        public static FlatReplacer Instance { get; private set; }
        private static Mod mod;
        private Dictionary<uint, FlatReplacement> flatReplacements;

        private struct FlatReplacementRecord
        {
            public int[] Regions;
            public int FactionId;
            public int QualityMin;
            public int QualityMax;
            public int ChanceToReplace;
            public int TextureArchive;
            public int TextureRecord;
            public string FlatTextureName;
            public int FlatPortrait;
        }

        private struct FlatReplacement
        {
            public FlatReplacementRecord Record;
            public Texture2D[] AnimationFrames;
        }

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<FlatReplacer>();
        }

        private void Start()
        {
            // Read flat definition
            const string replacementDirectory = "FlatReplacements";
            var replacementPath = Path.Combine(Application.streamingAssetsPath, replacementDirectory);
            if (!Directory.Exists(replacementPath))
                return; // Don't do anything without this folder.
            var replacementFiles = Directory.GetFiles(replacementPath);
            var serializer = new fsSerializer();
            var texturesDirectory = Path.Combine(Application.streamingAssetsPath, "Textures");
            List<FlatReplacementRecord> replacementRecords = null;
            flatReplacements = new Dictionary<uint, FlatReplacement>();
            var textureCache = new Dictionary<string, Texture2D>();
            foreach (var replacementFile in replacementFiles)
            {
                using (var streamReader = new StreamReader(replacementFile))
                {
                    var fsResult = fsJsonParser.Parse(streamReader.ReadToEnd(), out var fsData); // Parse whole file.
                    if (!fsResult.Equals(fsResult.Success))
                        continue;
                    serializer.TryDeserialize(fsData, ref replacementRecords).AssertSuccess();
                }

                // Load flat graphics
                foreach (var record in replacementRecords)
                {
                    var key = ((uint)record.TextureArchive << 16) + (uint)record.TextureRecord; // Pack archive and record into single unsigned 32-bit integer
                    if (flatReplacements.ContainsKey(key))
                    {
                        Debug.Log($"FlatReplacer: Conflict -> {flatReplacements[key].Record.FlatTextureName} and {record.FlatTextureName} both replace the same flat. Ignoring the latter.");
                        continue;
                    }

                    // Reserve a frame for each numbered texture file starting with 0.
                    var animationFrameCount = 0;
                    while (File.Exists(Path.Combine(texturesDirectory, $"{record.FlatTextureName}{animationFrameCount}.png")))
                        animationFrameCount++;
                    if (animationFrameCount == 0) // Don't make an entry with no textures.
                        continue;
                    flatReplacements[key] = new FlatReplacement() { Record = record, AnimationFrames = new Texture2D[animationFrameCount] };
                    for (var i = 0; i < animationFrameCount; i++)
                    {
                        var textureName = $"{record.FlatTextureName}{i}";
                        if (TryGetTexture(textureName, textureCache, out var texture))
                        {
                            texture.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
                            flatReplacements[key].AnimationFrames[i] = texture;
                        }
                        else
                            Debug.Log($"Failed to load {textureName}.");
                    }
                }
            }

            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            DaggerfallUI.UIManager.OnWindowChange += OnWindowChange;
        }

        private bool TryGetTexture(string name, Dictionary<string, Texture2D> textureCache, out Texture2D texture)
        {
            // Use cached reference to prevent duplication.
            if (textureCache.ContainsKey(name))
            {
                texture = textureCache[name];
                return true;
            }

            // Pull from mods/loose files.
            var success = TextureReplacement.TryImportTexture(name, true, out texture);
            if (success)
                textureCache.Add(name, texture);
            return success;
        }

        private void OnWindowChange(object sender, System.EventArgs e)
        {
            if (DaggerfallUI.UIManager.TopWindow != DaggerfallUI.Instance.TalkWindow || !TalkManager.Instance.StaticNPC)
                return;
            // The last vanilla portrait ID is 502
            var replacementBillboard = TalkManager.Instance.StaticNPC.gameObject.GetComponent<ReplacementBillboard>();
            var facePortraitArchive = DaggerfallWorkshop.Game.UserInterface.DaggerfallTalkWindow.FacePortraitArchive.CommonFaces;
            GameManager.Instance.PlayerEntity.FactionData.GetFactionData(TalkManager.Instance.StaticNPC.Data.factionID, out var factionData);
            if (factionData.type == 4 && factionData.face <= 60)
                facePortraitArchive = DaggerfallWorkshop.Game.UserInterface.DaggerfallTalkWindow.FacePortraitArchive.SpecialFaces;
            if (replacementBillboard && replacementBillboard.HasCustomPortrait)
                DaggerfallUI.Instance.TalkWindow.SetNPCPortrait(facePortraitArchive, replacementBillboard.CustomPortraitRecord);
        }

        private void OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            var gameManager = GameManager.Instance;
            var playerGps = gameManager.PlayerGPS;
            var scene = gameManager.InteriorParent.transform.Find(DaggerfallInterior.GetSceneName(playerGps.CurrentLocation, args.StaticDoor));
            var npcTransforms = scene.transform.Find("People Flats");
            foreach (Transform npcTransform in npcTransforms)
            {
                // Discard vanilla billboard if there is a replacement loaded that fits criteria.
                var go = npcTransform.gameObject;
                if (!go)
                    continue;
                var billboard = go.GetComponent<DaggerfallBillboard>();
                if (!billboard)
                    continue;
                var archive = billboard.Summary.Archive;
                var record = billboard.Summary.Record;
                var key = ((uint)archive << 16) + (uint)record;
                if (!flatReplacements.ContainsKey(key))
                    continue; // Nothing to replace this with.
                var regionFound = false;
                if (flatReplacements[key].Record.Regions[0] == -1)
                    regionFound = true; // -1 = region wildcard
                else
                    foreach (var regionIndex in flatReplacements[key].Record.Regions)
                    {
                        if (playerGps.CurrentRegionIndex == regionIndex)
                        {
                            regionFound = true;
                            break;
                        }
                    }

                if (!regionFound)
                    continue; // Don't replace if outside specified regions.
                var buildingData = gameManager.PlayerEnterExit.BuildingDiscoveryData;
                if (flatReplacements[key].Record.FactionId != -1 && flatReplacements[key].Record.FactionId != buildingData.factionID)
                    continue; // Don't replace if faction-specific. -1 indicates faction-agnostic.
                if (buildingData.quality < flatReplacements[key].Record.QualityMin || buildingData.quality > flatReplacements[key].Record.QualityMax)
                    continue; // Don't replace if outside building quality range.
                // Don't replace if chance to replace isn't hit. Result is pseudo-random, same every time for any given static NPC.
                var staticNpc = go.GetComponent<StaticNPC>();
                // TODO: new-ing a random class probably has some overhead. Find leaner way to do this. Preferably stack-only.
                var random100 = flatReplacements[key].Record.ChanceToReplace == 100 ? 0 : (uint)new System.Random(staticNpc.Data.nameSeed).Next() % 100;
                #if UNITY_EDITOR
                Debug.Log($"Replacement for flat {flatReplacements[key].Record.TextureArchive}-{flatReplacements[key].Record.TextureRecord} ({flatReplacements[key].Record.FlatTextureName}) detected. {flatReplacements[key].Record.ChanceToReplace}% chance. Building quality: {buildingData.quality}. Roll: {random100} = {(random100 <= flatReplacements[key].Record.ChanceToReplace ? "success" : "fail")}.");
                #endif
                if (!staticNpc || random100 > flatReplacements[key].Record.ChanceToReplace)
                    continue;
                Destroy(billboard);
                // Use custom implementation.
                var replacementBillboard = go.AddComponent<ReplacementBillboard>();
                replacementBillboard.SetMaterial(flatReplacements[key].Record.FlatTextureName, new Vector2(flatReplacements[key].AnimationFrames[0].width, flatReplacements[key].AnimationFrames[0].height), flatReplacements[key].AnimationFrames);
                if (flatReplacements[key].Record.FlatPortrait > -1)
                {
                    replacementBillboard.HasCustomPortrait = true;
                    replacementBillboard.CustomPortraitRecord = flatReplacements[key].Record.FlatPortrait;
                }
            }
        }
    }
}
