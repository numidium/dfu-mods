using DaggerfallWorkshop;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureShock
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ImpactBillboard : MonoBehaviour
    {
        private Camera mainCamera = null;
        private MeshRenderer meshRenderer;
        private const float frameTime = 0.0625f;
        private float frameTimeRemaining = 0f;
        private int currentFrame = -1;
        private Texture2D[] frames;

        private void Start()
        {
            if (Application.isPlaying)
            {
                mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
                meshRenderer = GetComponent<MeshRenderer>();
            }
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
                        Destroy(gameObject); // Animation is finished and so is this object.
                    else
                        meshRenderer.material.SetTexture(Uniforms.MainTex, frames[currentFrame]);
                }
                else
                    frameTimeRemaining -= Time.deltaTime;
            }
        }

        public bool SetFrames(Texture2D[] textures)
        {
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            if (!dfUnity.IsReady)
                return false;
            meshRenderer = GetComponent<MeshRenderer>();
            var material = MaterialReader.CreateBillboardMaterial();
            material.mainTexture = textures[0];
            var mesh = dfUnity.MeshReader.GetSimpleBillboardMesh(new Vector2(.5f, .5f));
            var meshFilter = GetComponent<MeshFilter>();
            var oldMesh = meshFilter.sharedMesh;
            if (mesh)
            {
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
            }

            if (oldMesh)
                Destroy(oldMesh);
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            frames = textures;
            return true;
        }
    }
}
