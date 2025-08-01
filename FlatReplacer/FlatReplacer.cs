using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using FullSerializer;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FlatReplacer
{
    public sealed class FlatReplacer : MonoBehaviour
    {
        private const string modSignature = "Flat Replacer";
        const int wildcardOrDefault = -1;
        public static FlatReplacer Instance { get; private set; }
        private static Mod mod;
        private Dictionary<uint, FlatReplacement[]> flatReplacements;
        private bool replacementsRequested;
        private StaticDoor lastDoor;

        private class FlatReplacement
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
            List<FlatReplacementRecord> replacementRecords;
            var replacements = new Dictionary<uint, List<FlatReplacement>>();
            var textureCache = new Dictionary<string, Texture2D>();
            foreach (var replacementFile in replacementFiles)
            {
                using (var streamReader = new StreamReader(replacementFile))
                {
                    var fsResult = fsJsonParser.Parse(streamReader.ReadToEnd(), out var fsData); // Parse whole file.
                    if (!fsResult.Equals(fsResult.Success))
                        continue;
                    replacementRecords = null;
                    serializer.TryDeserialize(fsData, ref replacementRecords).AssertSuccess();
                }

                // Load flat graphics
                foreach (var record in replacementRecords)
                {
                    if (record.TextureArchive == wildcardOrDefault || record.TextureRecord == wildcardOrDefault)
                    {
                        PrintLogText($"Undefined texture lookup.");
                        continue;
                    }

                    var key = ((uint)record.TextureArchive << 16) + (uint)record.TextureRecord; // Pack archive and record into single unsigned 32-bit integer
                    if (!replacements.ContainsKey(key))
                        replacements[key] = new List<FlatReplacement>();
                    var isValidVanillaFlat = record.ReplaceTextureArchive > 0 && record.ReplaceTextureRecord > wildcardOrDefault;
                    var isValidCustomFlat = true;
                    var animationFrames = new List<Texture2D>();
                    var textureHasCustomName = record.FlatTextureName != string.Empty && record.FlatTextureName != null;
                    if (!isValidVanillaFlat && textureHasCustomName)
                    {
                        while (TryGetTexture($"{record.FlatTextureName}{animationFrames.Count}", textureCache, out var texture))
                        {
                            texture.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
                            animationFrames.Add(texture);
                        }

                        if (animationFrames.Count < 1)
                        {
                            PrintLogText($"Failed to load custom texture: {record.FlatTextureName}.");
                            isValidCustomFlat = false;
                        }
                    }

                    var customAnimation = isValidCustomFlat && !isValidVanillaFlat && textureHasCustomName ? animationFrames.ToArray() : null;
                    if (isValidCustomFlat || isValidVanillaFlat)
                        replacements[key].Add(new FlatReplacement() { Record = record, AnimationFrames = customAnimation });
                    else
                        PrintLogText($"Failed to add replacement for {record.TextureArchive}-{record.TextureRecord} -> '{record.FlatTextureName}', ({record.ReplaceTextureArchive}-{record.ReplaceTextureRecord})");
                }
            }

            // convert to a dict of arrays
            flatReplacements = new Dictionary<uint, FlatReplacement[]>();
            foreach (var key in replacements.Keys)
            {
                flatReplacements.Add(key, replacements[key].ToArray());
            }

            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            DaggerfallUI.UIManager.OnWindowChange += OnWindowChange;
            Debug.Log($"{modSignature} initialized");
        }

        private void LateUpdate()
        {
            if (!replacementsRequested)
                return;
            var gameManager = GameManager.Instance;
            var playerGps = gameManager.PlayerGPS;
            var buildingData = gameManager.PlayerEnterExit.BuildingDiscoveryData;
            Transform scene = null;
            Transform npcTransforms;
            if (lastDoor.doorType == DoorTypes.DungeonEntrance)
            {
                foreach (Transform transform in gameManager.DungeonParent.transform)
                {
                    if (transform.name.StartsWith("DaggerfallDungeon"))
                    {
                        scene = transform;
                        break;
                    }
                }

                // step through dungeon blocks
                foreach (Transform block in scene)
                {
                    npcTransforms = block.Find("Flats").transform;
                    if (npcTransforms == null)
                        continue;
                    foreach (Transform npcTransform in npcTransforms)
                        ReplaceBillboard(npcTransform, playerGps, gameManager, in buildingData, LocationTypes.Dungeon);
                }

                replacementsRequested = false;
            }
            else
            {
                scene = gameManager.InteriorParent.transform.Find(DaggerfallInterior.GetSceneName(playerGps.CurrentLocation, lastDoor));
                npcTransforms = scene.transform.Find("People Flats");
                if (npcTransforms == null)
                {
                    replacementsRequested = false;
                    return;
                }

                foreach (Transform npcTransform in npcTransforms)
                    ReplaceBillboard(npcTransform, playerGps, gameManager, in buildingData, LocationTypes.Building);
                replacementsRequested = false;
            }
        }

        private void ReplaceBillboard(Transform npcTransform, PlayerGPS playerGps, GameManager gameManager, in PlayerGPS.DiscoveredBuilding buildingData, LocationTypes enteredLocationType)
        {
            // Discard vanilla billboard if there is a replacement loaded that fits criteria.
            var go = npcTransform.gameObject;
            if (!go)
                return;
            var billboard = go.GetComponent<Billboard>();
            if (!billboard)
                return;
            var archive = billboard.Summary.Archive;
            var record = billboard.Summary.Record;
            var npcFactionId = billboard.Summary.FactionOrMobileID;
            var key = ((uint)archive << 16) + (uint)record;
            if (!flatReplacements.ContainsKey(key))
                return; // Nothing to replace this with.
            var candidates = new List<byte>();
            for (var i = (byte)0; i < flatReplacements[key].Length; i++)
            {
                var replacementRecord = flatReplacements[key][i].Record;
                if (replacementRecord.LocationTypes != (int)LocationTypes.All && (replacementRecord.LocationTypes & (int)enteredLocationType) == 0)
                    continue;
                var regionFound = false;
                if (replacementRecord.Regions[0] == wildcardOrDefault)
                    regionFound = true;
                else
                    foreach (var regionIndex in replacementRecord.Regions)
                    {
                        if (playerGps.CurrentRegionIndex == regionIndex)
                        {
                            regionFound = true;
                            break;
                        }
                    }

                if (!regionFound)
                    continue; // Don't replace if outside specified regions.
                gameManager.PlayerEntity.FactionData.GetFactionData(npcFactionId, out var factionData);
                var buildingDoesNotMatch = false;
                if (enteredLocationType == LocationTypes.Building)
                {
                    buildingDoesNotMatch =
                        // Don't replace if faction-specific.
                        ((replacementRecord.FactionId != wildcardOrDefault && replacementRecord.FactionId != buildingData.factionID) ||
                        // Don't replace if building type does not match.
                        (replacementRecord.BuildingType != wildcardOrDefault && replacementRecord.BuildingType != (int)buildingData.buildingType) ||
                        // Don't replace if outside building quality range.
                        (buildingData.quality < replacementRecord.QualityMin || buildingData.quality > replacementRecord.QualityMax));
                }

                if (buildingDoesNotMatch || (replacementRecord.SocialGroup != wildcardOrDefault && replacementRecord.SocialGroup != factionData.sgroup))
                    continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0)
                return;
            var staticNpc = go.GetComponent<StaticNPC>();
            // Pick a random replacement from any that match the criteria.
            var randomNumber = candidates.Count > 1 ? new System.Random(staticNpc.Data.nameSeed).Next() : 0;
            var chosenIndex = candidates[randomNumber % candidates.Count];
            var chosenReplacement = flatReplacements[key][chosenIndex];
            var chosenReplacementRecord = chosenReplacement.Record;
            if (chosenReplacement.Record.NameBank != wildcardOrDefault)
            {
                if (System.Enum.IsDefined(typeof(NameHelper.BankTypes), chosenReplacementRecord.NameBank))
                    staticNpc.NameBank = (NameHelper.BankTypes)chosenReplacementRecord.NameBank;
                else
                    PrintLogText($"Invalid name bank index: {chosenReplacementRecord.NameBank}");
            }
#if UNITY_EDITOR
            var logText = $"Replacement for flat {chosenReplacementRecord.TextureArchive}-{chosenReplacementRecord.TextureRecord} ";
            if (chosenReplacementRecord.ReplaceTextureArchive > 0 || chosenReplacementRecord.ReplaceTextureRecord > wildcardOrDefault)
                logText += $"(Vanilla Texture: {chosenReplacementRecord.ReplaceTextureArchive}-{chosenReplacementRecord.ReplaceTextureRecord}) detected. ";
            else
                logText += $"(Custom Texture: {chosenReplacementRecord.FlatTextureName}) detected. ";
            logText += $"Building type: {buildingData.buildingType}, quality: {buildingData.quality}.";
            PrintLogText(logText);
#endif
            // Use custom billboard
            Material replacementMaterial = null;
            var replacementBillboard = go.AddComponent<ReplacementBillboard>();
            var oldSummary = billboard.Summary;
            if (chosenReplacement.AnimationFrames != null) // Custom graphics supplied with custom file name
            {
                replacementMaterial = replacementBillboard.SetMaterial(chosenReplacementRecord.FlatTextureName,
                    new Vector2(chosenReplacement.AnimationFrames[0].width, chosenReplacement.AnimationFrames[0].height),
                    chosenReplacement.AnimationFrames, chosenReplacementRecord.TextureArchive,
                    chosenReplacementRecord.TextureRecord, chosenReplacementRecord.UseExactDimensions);
            }
            else // Custom or vanilla graphics with vanilla filename
            {
                replacementMaterial = replacementBillboard.SetMaterial(in oldSummary,
                    chosenReplacementRecord.ReplaceTextureArchive, chosenReplacementRecord.ReplaceTextureRecord, chosenReplacementRecord.UseExactDimensions);
            }

            if (!replacementMaterial)
            {
                var errorText = $"The replacement flat for {chosenReplacementRecord.TextureArchive}-{chosenReplacementRecord.TextureRecord} -> ";
                if (chosenReplacementRecord.ReplaceTextureArchive > 0 || chosenReplacementRecord.ReplaceTextureRecord > wildcardOrDefault)
                    errorText += $"{chosenReplacementRecord.ReplaceTextureArchive}-{chosenReplacementRecord.ReplaceTextureRecord}";
                else
                    errorText += $"{chosenReplacementRecord.FlatTextureName}";
                errorText += " was not found. Ignoring...";
                PrintLogText(errorText);
                Destroy(replacementBillboard);
                return;
            }

            Destroy(billboard);
            var collider = go.GetComponent<BoxCollider>();
            var boundsResize = new Vector3(replacementBillboard.Summary.Size.x, replacementBillboard.Summary.Size.y, 0f);
            collider.size = boundsResize; // Resize collider to fit new graphics dimensions.
            var transformScale = go.GetComponent<Transform>().localScale;
            GameObjectHelper.AlignBillboardToGround(go, collider.size * new Vector2(transformScale.x, transformScale.y), 8f);
            if (chosenReplacementRecord.FlatPortrait > wildcardOrDefault)
            {
                replacementBillboard.HasCustomPortrait = true;
                replacementBillboard.CustomPortraitRecord = chosenReplacementRecord.FlatPortrait;
            }
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
            lastDoor = args.StaticDoor;
            replacementsRequested = true;
        }

        private void OnTransitionDungeonInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            lastDoor = args.StaticDoor;
            replacementsRequested = true;
        }

        private void PrintLogText(string text)
        {
            Debug.Log($"{modSignature}: {text}");
        }
    }
}
