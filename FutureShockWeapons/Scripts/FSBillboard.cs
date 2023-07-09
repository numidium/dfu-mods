using DaggerfallWorkshop;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureShock
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class FSBillboard : MonoBehaviour
    {
        private Camera mainCamera = null;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private const float frameTime = 0.0625f;
        private float frameTimeRemaining = 0f;
        private int currentFrame = -1;
        private Texture2D[] frames;
        private bool _isOneShot;
        private static readonly Dictionary<Texture2D, Material> materialLibrary = new Dictionary<Texture2D, Material>();

        private void Start()
        {
            if (Application.isPlaying)
                mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        }

        private void Update()
        {
            if (frames != null && mainCamera && Application.isPlaying && meshRenderer.enabled)
            {
                Vector3 viewDirection = -new Vector3(mainCamera.transform.forward.x, mainCamera.transform.forward.y, mainCamera.transform.forward.z);
                transform.LookAt(transform.position + viewDirection);
                if (frameTimeRemaining <= 0f)
                {
                    frameTimeRemaining = frameTime;
                    if (++currentFrame >= frames.Length)
                    {
                        if (_isOneShot)
                        {
                            // Animation is finished and so is this object.
                            DisposeAssets();
                            Destroy(gameObject);
                        }
                        else
                            currentFrame = 0;
                    }
                    else
                        meshRenderer.material.SetTexture(Uniforms.MainTex, frames[currentFrame]);
                }
                else
                    frameTimeRemaining -= Time.deltaTime;
            }
        }

        public bool SetFrames(Texture2D[] textures, Vector2 size, bool isOneShot = true)
        {
            var dfUnity = DaggerfallUnity.Instance;
            if (!dfUnity.IsReady)
                return false;
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.receiveShadows = false;
            meshFilter = GetComponent<MeshFilter>();
            if (!materialLibrary.ContainsKey(textures[0]))
            {
                materialLibrary[textures[0]] = MaterialReader.CreateBillboardMaterial();
                materialLibrary[textures[0]].mainTexture = textures[0];
            }

            var mesh = dfUnity.MeshReader.GetSimpleBillboardMesh(size);
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = materialLibrary[textures[0]];
            meshRenderer.shadowCastingMode = DaggerfallUnity.Settings.GeneralBillboardShadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
            frames = textures;
            _isOneShot = isOneShot;
            return true;
        }

        public void DisposeAssets()
        {
            Destroy(meshFilter.sharedMesh);
            Destroy(meshRenderer.material);
        }
    }
}
