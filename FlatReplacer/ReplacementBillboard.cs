using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Utility;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallConnect.Arena2;
using System.Linq;
using UnityEngine.Rendering;
using UnityEditor;

namespace FlatReplacer
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ReplacementBillboard : Billboard
    {
        private int framesPerSecond = 5;
        private float frameTime;
        private float frameTimeRemaining;
        private bool isOneShot = false;
        private bool faceY = false;
        private bool restartAnims = true;
        private MeshFilter meshFilter;
        private Camera mainCamera;
        private MeshRenderer meshRenderer;
        public override int FramesPerSecond { get => framesPerSecond; set { framesPerSecond = value; frameTime = 1f / framesPerSecond; } }
        public override bool OneShot { get => isOneShot; set { isOneShot = value; } }
        public override bool FaceY { get => faceY; set { faceY = value; } }
        public bool HasCustomPortrait { get; set; }
        public int CustomPortraitRecord { get; set; }

        private void Start()
        {
            frameTime = 1f / framesPerSecond;
            if (Application.isPlaying)
            {
                // Get component references
                mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
                meshFilter = GetComponent<MeshFilter>();
                meshRenderer = GetComponent<MeshRenderer>();

                // Hide editor marker from live scene
                bool showEditorFlats = GameManager.Instance.StartGameBehaviour.ShowEditorFlats;
                if (summary.FlatType == FlatTypes.Editor && meshRenderer && !showEditorFlats)
                {
                    // Just disable mesh renderer as actual object can be part of action chain
                    // Example is the treasury in Daggerfall castle, some action records flow through the quest item marker
                    meshRenderer.enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            restartAnims = true;
        }

        // Adapted from DaggerfallBillboard.cs
        private void Update()
        {
            // Restart animation coroutine if not running
            if (restartAnims && summary.AnimatedMaterial)
            {
                //StartCoroutine(AnimateBillboard());
                restartAnims = false;
            }

            // Rotate to face camera in game
            // Do not rotate if MeshRenderer disabled. The player can't see it anyway and this could be a hidden editor marker with child objects.
            // In the case of hidden editor markers with child treasure objects, we don't want a 3D replacement spinning around like a billboard.
            // Treasure objects are parented to editor marker in this way as the moving action data for treasure is actually on editor marker parent.
            // Visible child of treasure objects have their own MeshRenderer and DaggerfallBillboard to apply rotations.
            if (mainCamera && Application.isPlaying && meshRenderer.enabled)
            {
                var y = (FaceY) ? mainCamera.transform.forward.y : 0;
                var viewDirection = -new Vector3(mainCamera.transform.forward.x, y, mainCamera.transform.forward.z);
                transform.LookAt(transform.position + viewDirection);
            }

            if (!meshFilter)
                return;
            if (frameTimeRemaining <= 0f)
            {
                // Original Daggerfall textures
                if (!summary.ImportedTextures.HasImportedTextures)
                {
                    if (summary.AtlasIndices == null || summary.AtlasIndices.Length == 0)
                        return; // Can't do anything if atlas failed to load.
                    if (summary.CurrentFrame >= summary.AtlasIndices[summary.Record].frameCount)
                    {
                        summary.CurrentFrame = 0;
                        if (OneShot)
                            Destroy(gameObject);
                    }

                    var index = summary.AtlasIndices[summary.Record].startIndex + summary.CurrentFrame;
                    var rect = summary.AtlasRects[index];

                    // Update UVs on mesh
                    var uvs = new Vector2[4];
                    uvs[0] = new Vector2(rect.x, rect.yMax);
                    uvs[1] = new Vector2(rect.xMax, rect.yMax);
                    uvs[2] = new Vector2(rect.x, rect.y);
                    uvs[3] = new Vector2(rect.xMax, rect.y);
                    meshFilter.sharedMesh.uv = uvs;
                }
                // Custom textures
                else
                {
                    // Restart animation or destroy gameobject
                    // The game uses all -and only- textures found on disk, even if they are less or more than vanilla frames
                    if (summary.CurrentFrame >= summary.ImportedTextures.FrameCount)
                    {
                        summary.CurrentFrame = 0;
                        if (OneShot)
                            Destroy(gameObject);
                    }

                    // Set imported textures for current frame
                    meshRenderer.material.SetTexture(Uniforms.MainTex, summary.ImportedTextures.Albedo[summary.CurrentFrame]);
                    if (summary.ImportedTextures.IsEmissive)
                        meshRenderer.material.SetTexture(Uniforms.EmissionMap, summary.ImportedTextures.Emission[summary.CurrentFrame]);
                }

                summary.CurrentFrame++;
                frameTimeRemaining = frameTime;
            }
            else
                frameTimeRemaining -= Time.deltaTime;
        }

        // Taken from DaggerfallBillboard.cs and modified slightly
        void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            var scaledSize = summary.Size * transform.localScale;
            var sizeHalf = scaledSize * 0.5f;
            Handles.DrawWireDisc(transform.position - new Vector3(0, sizeHalf.y, 0), Vector3.up, sizeHalf.x);
#endif
        }

        // Adapted from DaggerfallBillboard.cs
        public Material SetMaterial(string namePrefix, Vector2 dimensions, Texture2D[] textures, int archive, int record, bool useExactDimensions)
        {
            // Get DaggerfallUnity
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            if (!dfUnity.IsReady)
                return null;

            // Get references
            meshRenderer = GetComponent<MeshRenderer>();

            Vector2 size = dimensions;
            Mesh mesh = null;
            summary.Rect.width = 1.0f;
            summary.Rect.height = 1.0f;
            if (useExactDimensions)
                mesh = CreateBillboardMesh(summary.Rect, size, Vector2.one, out size);
            else
                mesh = dfUnity.MeshReader.GetBillboardMesh(summary.Rect, archive, record, out size);
            summary.AtlasedMaterial = false;
            summary.AnimatedMaterial = summary.ImportedTextures.FrameCount > 1;

            // Set summary
            summary.FlatType = FlatTypes.NPC;
            summary.Size = size;

            // Set editor flat types
            if (summary.FlatType == FlatTypes.Editor)
                summary.EditorFlatType = MaterialReader.GetEditorFlatType(summary.Record);

            // Set NPC flat type based on archive
            if (RDBLayout.IsNPCFlat(summary.Archive))
                summary.FlatType = FlatTypes.NPC;

            // Assign mesh and material
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Mesh oldMesh = meshFilter.sharedMesh;
            var material = GetStaticBillboardMaterial(gameObject, namePrefix, ref summary, out Vector2 scale, textures);
            if (mesh)
            {
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
            }
            if (oldMesh)
            {
                // The old mesh is no longer required
#if UNITY_EDITOR
                DestroyImmediate(oldMesh);
#else
                Destroy(oldMesh);
#endif
            }

            // Add NPC trigger collider
            if (summary.FlatType == FlatTypes.NPC)
            {
                var col = gameObject.GetComponent<BoxCollider>();
                if (col)
                    col.isTrigger = true;
            }

            return material;
        }

        // Adapted from TextureReplacement.cs
        public static Material GetStaticBillboardMaterial(GameObject go, string namePrefix, ref BillboardSummary summary, out Vector2 scale, Texture2D[] textures)
        {
            scale = Vector2.one;

            if (!DaggerfallUnity.Settings.AssetInjection)
                return null;
            summary.ImportedTextures.HasImportedTextures = true;
            Texture2D albedo = textures[0];

            // Read xml configuration
            Vector2 uv = Vector2.zero;
            XMLManager xml;
            if (XMLManager.TryReadXml(TextureReplacement.TexturesPath, namePrefix, out xml))
            {
                // Set billboard scale
                Transform transform = go.GetComponent<Transform>();
                scale = transform.localScale = xml.GetVector3("scaleX", "scaleY", transform.localScale);

                // Get UV
                uv = xml.GetVector2("uvX", "uvY", uv);
            }

            // Make material
            Material material = MaterialReader.CreateBillboardMaterial();
            summary.Rect = new Rect(uv.x, uv.y, 1 - 2 * uv.x, 1 - 2 * uv.y);

            // Set textures on material; emission is always overriden, with actual texture or null.
            material.SetTexture(Uniforms.MainTex, albedo);
            //material.SetTexture(Uniforms.EmissionMap, albedo); // just use albedo for emission

            // Save results
            summary.ImportedTextures.FrameCount = textures.Length;
            summary.ImportedTextures.IsEmissive = false;
            summary.ImportedTextures.Albedo = textures.ToList();
            //summary.ImportedTextures.Emission = emissionTextures;

            return material;
        }

        // Adapted from DaggerfallBillboard.cs
        public Mesh CreateBillboardMesh(Rect rect, Vector2 size, Vector2 scale, out Vector2 sizeOut)
        {
            // Apply scale
            Vector2 finalSize;
            int xChange = (int)(size.x * (scale.x / BlocksFile.ScaleDivisor));
            int yChange = (int)(size.y * (scale.y / BlocksFile.ScaleDivisor));
            finalSize.x = (size.x + xChange);
            finalSize.y = (size.y + yChange);

            // Store sizeOut
            sizeOut = finalSize * MeshReader.GlobalScale;

            // Vertices
            float hx = (finalSize.x / 2) * MeshReader.GlobalScale;
            float hy = (finalSize.y / 2) * MeshReader.GlobalScale;
            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(hx, hy, 0);// + offset;
            vertices[1] = new Vector3(-hx, hy, 0);// + offset;
            vertices[2] = new Vector3(hx, -hy, 0);// + offset;
            vertices[3] = new Vector3(-hx, -hy, 0);// + offset;

            // Indices
            int[] indices = new int[6]
            {
                0, 1, 2,
                3, 2, 1,
            };

            // Normals
            // Setting in between forward and up so billboards will
            // pick up some light from both above and in front.
            // This seems to work generally well for both directional and point lights.
            // Possibly need a better solution later.
            Vector3 normal = Vector3.Normalize(Vector3.up + Vector3.forward);
            Vector3[] normals = new Vector3[4];
            normals[0] = normal;
            normals[1] = normal;
            normals[2] = normal;
            normals[3] = normal;

            // UVs
            Vector2[] uvs = new Vector2[4];
            uvs[0] = new Vector2(rect.x, rect.yMax);
            uvs[1] = new Vector2(rect.xMax, rect.yMax);
            uvs[2] = new Vector2(rect.x, rect.y);
            uvs[3] = new Vector2(rect.xMax, rect.y);

            // Create mesh
            Mesh mesh = new Mesh
            {
                name = string.Format("BillboardMesh"),
                vertices = vertices,
                triangles = indices,
                normals = normals,
                uv = uvs
            };

            return mesh;
        }

        // Adapted from DaggerfallBillboard.cs
        public override void AlignToBase()
        {
            Vector3 offset = Vector3.zero;
            offset.y = (summary.Size.y / 2);
            transform.position += offset;
        }

        // Adapted from DaggerfallBillboard.cs
        public override void SetRDBResourceData(DFBlock.RdbFlatResource resource)
        {
            // Add common data
            summary.Flags = resource.Flags;
            summary.FactionOrMobileID = (int)resource.FactionOrMobileId;
            summary.FixedEnemyType = MobileTypes.None;

            // This is never used.
            summary.NameSeed = (int)resource.Position;

            // Set data of fixed mobile types (e.g. non-random enemy spawn)
            if (resource.TextureArchive == 199)
            {
                if (resource.TextureRecord == 16)
                {
                    summary.IsMobile = true;
                    summary.EditorFlatType = EditorFlatTypes.FixedMobile;

                    bool isCustomMarker = resource.IsCustomData;
                    if (!isCustomMarker)
                        summary.FixedEnemyType = (MobileTypes)(summary.FactionOrMobileID & 0xff);
                    else
                        summary.FixedEnemyType = (MobileTypes)(summary.FactionOrMobileID);
                }
                else if (resource.TextureRecord == 10) // Start marker. Holds data for dungeon block water level and castle block status.
                {
                    if (resource.SoundIndex != 0)
                        summary.WaterLevel = (short)(-8 * resource.SoundIndex);
                    else
                        summary.WaterLevel = 10000; // no water

                    summary.CastleBlock = (resource.Magnitude != 0);
                }
            }
        }

        public override void SetRMBPeopleData(DFBlock.RmbBlockPeopleRecord person)
        {
            SetRMBPeopleData(person.FactionID, person.Flags, person.Position);
        }

        public override void SetRMBPeopleData(int factionID, int flags, long position = 0)
        {
            // Add common data
            summary.FactionOrMobileID = factionID;
            summary.FixedEnemyType = MobileTypes.None;
            summary.Flags = flags;

            // This is never used.
            summary.NameSeed = (int)position;
        }

        // Copied from DaggerfallBillboard.cs (slightly modified)
        public override Material SetMaterial(int archive, int record, int frame = 0)
        {
            // Get DaggerfallUnity
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            if (!dfUnity.IsReady)
                return null;

            // Get references
            meshRenderer = GetComponent<MeshRenderer>();

            Vector2 size;
            Vector2 scale;
            Mesh mesh = null;
            Material material = null;
            if (material = TextureReplacement.GetStaticBillboardMaterial(gameObject, archive, record, ref summary, out scale))
            {
                mesh = dfUnity.MeshReader.GetBillboardMesh(summary.Rect, summary.Archive, summary.Record, out size);
                summary.AtlasedMaterial = false;
                summary.AnimatedMaterial = summary.ImportedTextures.FrameCount > 1;
            }
            else if (dfUnity.MaterialReader.AtlasTextures)
            {
                material = dfUnity.MaterialReader.GetMaterialAtlas(
                    archive,
                    0,
                    4,
                    2048,
                    out summary.AtlasRects,
                    out summary.AtlasIndices,
                    4,
                    true,
                    0,
                    false,
                    true);
                if (record >= summary.AtlasIndices.Length)
                {
                    return null; // Invalid record specified.
                }

                mesh = dfUnity.MeshReader.GetBillboardMesh(
                    summary.AtlasRects[summary.AtlasIndices[record].startIndex],
                    archive,
                    record,
                    out size);
                summary.AtlasedMaterial = true;
                if (summary.AtlasIndices[record].frameCount > 1)
                    summary.AnimatedMaterial = true;
                else
                    summary.AnimatedMaterial = false;
            }
            else
            {
                material = dfUnity.MaterialReader.GetMaterial(
                    archive,
                    record,
                    frame,
                    0,
                    out summary.Rect,
                    4,
                    true,
                    true);
                mesh = dfUnity.MeshReader.GetBillboardMesh(
                    summary.Rect,
                    archive,
                    record,
                    out size);
                summary.AtlasedMaterial = false;
                summary.AnimatedMaterial = false;
            }

            // Set summary
            summary.FlatType = MaterialReader.GetFlatType(archive);
            summary.Archive = archive;
            summary.Record = record;
            summary.Size = size;

            // Set editor flat types
            if (summary.FlatType == FlatTypes.Editor)
                summary.EditorFlatType = MaterialReader.GetEditorFlatType(summary.Record);

            // Set NPC flat type based on archive
            if (RDBLayout.IsNPCFlat(summary.Archive))
                summary.FlatType = FlatTypes.NPC;

            // Assign mesh and material
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Mesh oldMesh = meshFilter.sharedMesh;
            if (mesh)
            {
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
            }
            if (oldMesh)
            {
                // The old mesh is no longer required
#if UNITY_EDITOR
                DestroyImmediate(oldMesh);
#else
                Destroy(oldMesh);
#endif
            }

            // General billboard shadows if enabled
            bool isLightArchive = (archive == TextureReader.LightsTextureArchive);
            meshRenderer.shadowCastingMode = (DaggerfallUnity.Settings.GeneralBillboardShadows && !isLightArchive) ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;

            // Add NPC trigger collider
            if (summary.FlatType == FlatTypes.NPC)
            {
                Collider col = gameObject.GetComponent<BoxCollider>();
                if (col == null)
                    col = gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
            }

            return material;
        }

        public Material SetMaterial(in BillboardSummary oldSummary, int archive, int record, bool useExactDimensions, int frame = 0)
        {
            // Get DaggerfallUnity
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            if (!dfUnity.IsReady)
                return null;

            // Assign mesh and material
            var meshArchive = useExactDimensions ? archive : oldSummary.Archive;
            var meshRecord = useExactDimensions ? record : oldSummary.Record;
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Mesh oldMesh = meshFilter.sharedMesh;
            Mesh mesh = null;
            Vector2 size;
            Material material = null;
            if (material = TextureReplacement.GetStaticBillboardMaterial(gameObject, archive, record, ref summary, out Vector2 scale))
            {
                mesh = dfUnity.MeshReader.GetBillboardMesh(summary.Rect, meshArchive, meshRecord, out size);
                summary.AtlasedMaterial = false;
                summary.AnimatedMaterial = summary.ImportedTextures.FrameCount > 1;
            }
            else if (dfUnity.MaterialReader.AtlasTextures)
            {
                material = dfUnity.MaterialReader.GetMaterialAtlas(
                    archive,
                    0,
                    4,
                    2048,
                    out summary.AtlasRects,
                    out summary.AtlasIndices,
                    4,
                    true,
                    0,
                    false,
                    true);
                if (record >= summary.AtlasIndices.Length)
                {
                    return null; // Invalid record specified.
                }

                mesh = dfUnity.MeshReader.GetBillboardMesh(
                    summary.AtlasRects[summary.AtlasIndices[record].startIndex],
                    meshArchive,
                    meshRecord,
                    out size);
                summary.AtlasedMaterial = true;
                if (summary.AtlasIndices[record].frameCount > 1)
                    summary.AnimatedMaterial = true;
                else
                    summary.AnimatedMaterial = false;
            }
            else
            {
                material = dfUnity.MaterialReader.GetMaterial(
                    meshArchive,
                    meshRecord,
                    frame,
                    0,
                    out summary.Rect,
                    4,
                    true,
                    true);
                mesh = dfUnity.MeshReader.GetBillboardMesh(
                    summary.Rect,
                    meshArchive,
                    meshRecord,
                    out size);
                summary.AtlasedMaterial = false;
                summary.AnimatedMaterial = false;
            }

            // Get references
            meshRenderer = GetComponent<MeshRenderer>();

            // Set summary
            summary.Archive = archive;
            summary.Record = record;
            summary.Size = size;

            if (mesh)
            {
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
            }
            if (oldMesh)
            {
                // The old mesh is no longer required
#if UNITY_EDITOR
                DestroyImmediate(oldMesh);
#else
                Destroy(oldMesh);
#endif
            }

            // Add NPC trigger collider
            if (summary.FlatType == FlatTypes.NPC)
            {
                var col = gameObject.GetComponent<BoxCollider>();
                if (col)
                    col.isTrigger = true;
            }

            return material;
        }

        public override Material SetMaterial(Texture2D texture, Vector2 size, bool isLightArchive = false)
        {
            throw new System.NotImplementedException();
        }

        public void SetSummary(BillboardSummary billboardSummary)
        {
            summary = billboardSummary;
        }
    }
}
