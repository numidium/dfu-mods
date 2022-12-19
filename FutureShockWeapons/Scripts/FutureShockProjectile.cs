using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace FutureShock
{
    // Credits: Interkarma and Allofich for their work on DaggerfallMissile.cs, which this class is based on.
    [RequireComponent(typeof(Light))]
    [RequireComponent(typeof(AudioSource))]
    public sealed class FutureShockProjectile : MonoBehaviour
    {
        public float Velocity { private get; set; }
        public float CollisionRadius = 0.35f;
        public float ExplosionRadius = 5.0f;
        public bool EnableLight = true;
        public bool EnableShadows = true;
        public float LifespanInSeconds = 8f;
        public float PostImpactLifespanInSeconds = 0.6f;
        public float PostImpactLightMultiplier = 1.6f;
        public float PostImpactFade = 4f;
        public DaggerfallUnityItem OriginWeapon { private get; set; }
        public float HorizontalAdjust { private get; set; }
        public float VerticalAdjust { private get; set; }
        public bool IsExplosive { get; set; }
        public bool IsGrenade { get; set; }
        public Color LightColor { private get; set; }
        public Texture2D ProjectileTexture { private get; set; }
        public DaggerfallEntityBehaviour Caster { get; set; }
        private Vector3 direction;
        private Vector3 collisionPosition;
        private Light myLight;
        private AudioSource audioSource;
        private AudioClip impactSound;
        private AudioClip travelSound;
        private bool travelSoundIsLooped;
        private float lifespan = 0f;
        private float postImpactLifespan = 0f;
        private bool missileReleased = false;
        private bool impactDetected = false;
        private bool impactAssigned = false;
        private bool postImpactLightAssigned = false;
        private float initialRange;
        private float initialIntensity;
        private GameObject goProjectile = null;
        private Texture2D[] impactFrames;
        private Texture2D[] projectileFrames;
        private Vector2 impactSize;
        private Vector2 projectileSize;
        private int playerLayerMask;
        private float downwardCurve = .2f;
        private bool isWaitTick = false;
        private const float grenadeGravity = .05f;
        private bool isImpactBillboardRequested = false;

        public void SetImpactFrames(Texture2D[] frames, Vector2 size)
        {
            impactFrames = frames;
            impactSize = size;
        }

        public void SetProjectileFrames(Texture2D[] frames, Vector2 size)
        {
            projectileFrames = frames;
            projectileSize = size;
        }

        public void SetSounds(AudioClip travel, AudioClip impact, bool travelIsLooped)
        {
            travelSound = travel;
            impactSound = impact;
            travelSoundIsLooped = travelIsLooped;
        }

        private void Awake()
        {
            audioSource = transform.GetComponent<AudioSource>();
        }

        private void Start()
        {
            // Setup light and shadows
            myLight = GetComponent<Light>();
            myLight.enabled = EnableLight;
            myLight.color = LightColor;
            if (!DaggerfallUnity.Settings.EnableSpellShadows) myLight.shadows = LightShadows.None;
            initialRange = myLight.range;
            initialIntensity = myLight.intensity;

            // Setup projectile
            Vector3 adjust;
            // Adjust to fit gun HUD position.
            adjust = (GameManager.Instance.MainCamera.transform.rotation * -Caster.transform.up) * VerticalAdjust;
            if (!IsGrenade) // Grenades use 2D projectiles
            {
                goProjectile = GameObjectHelper.CreateDaggerfallMeshGameObject(99800, transform, ignoreCollider: true); // TODO: Use proper models
                var meshRenderer = goProjectile.GetComponent<MeshRenderer>();
                if (meshRenderer)
                {
                    meshRenderer.sharedMaterials[0].mainTexture = ProjectileTexture;
                    meshRenderer.sharedMaterials[1].mainTexture = ProjectileTexture;
                }

                if (!GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal)
                    adjust += GameManager.Instance.MainCamera.transform.right * HorizontalAdjust;
                else
                    adjust -= GameManager.Instance.MainCamera.transform.right * HorizontalAdjust;
                goProjectile.transform.localPosition = adjust;
                goProjectile.transform.rotation = Quaternion.LookRotation(GameManager.Instance.MainCamera.transform.forward);
                goProjectile.layer = gameObject.layer;
            }
            else
            {
                if (!GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal)
                    adjust += GameManager.Instance.MainCamera.transform.right * HorizontalAdjust;
                else
                    adjust -= GameManager.Instance.MainCamera.transform.right * HorizontalAdjust;
                goProjectile = new GameObject("FlatProjectile");
                goProjectile.transform.parent = gameObject.transform;
                goProjectile.transform.localPosition = adjust;
                goProjectile.transform.rotation = Quaternion.LookRotation(GameManager.Instance.MainCamera.transform.forward);
                goProjectile.layer = gameObject.layer;
                var flatProjectile = goProjectile.AddComponent<FSBillboard>();
                flatProjectile.SetFrames(projectileFrames, projectileSize, false);
            }

            if (travelSound)
                PlaySound(travelSound, 0.6f, travelSoundIsLooped);
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
        }

        private void Update()
        {
            if (!missileReleased)
            {
                direction = GameManager.Instance.MainCamera.transform.forward;
                transform.position = GameManager.Instance.MainCamera.transform.position;
                collisionPosition = transform.position;
                missileReleased = true;
            }

            var frameDeltaTime = Time.deltaTime;
            if (!impactDetected)
            {
                // Transform missile along direction vector
                transform.position += (direction * Velocity) * frameDeltaTime;
                if (IsGrenade)
                    direction += Vector3.down * (downwardCurve * frameDeltaTime);
                if (lifespan > LifespanInSeconds)
                    Destroy(gameObject);
            }
            else
            {
                transform.position = collisionPosition; // Match visual position with collider position.
                if (!impactAssigned)
                {
                    if (impactSound)
                        PlaySound(impactSound, 0.8f);
                    if (isImpactBillboardRequested)
                    {
                        var go = new GameObject("ImpactBillboard");
                        go.transform.parent = transform;
                        go.transform.localPosition = Vector3.zero;
                        go.layer = gameObject.layer;
                        var billboard = go.AddComponent<FSBillboard>();
                        billboard.SetFrames(impactFrames, impactSize);
                    }

                    impactAssigned = true;
                }

                // Light
                if (EnableLight)
                {
                    myLight.range = initialRange * PostImpactLightMultiplier;
                    if (!postImpactLightAssigned)
                    {
                        myLight.intensity = initialIntensity * PostImpactLightMultiplier;
                        postImpactLightAssigned = true;
                    }
                    else
                    {
                        // Fade out light.
                        myLight.intensity -= PostImpactFade * frameDeltaTime;
                    }
                }

                // Wait for light.
                postImpactLifespan += frameDeltaTime;
                if (postImpactLifespan > PostImpactLifespanInSeconds)
                {
                    myLight.enabled = false;
                    // Wait for audio clip.
                    if (audioSource && !audioSource.isPlaying)
                        Destroy(gameObject);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!missileReleased || impactDetected)
                return;
            lifespan += Time.fixedDeltaTime;
            var tickTime = Time.fixedDeltaTime * 2f; // Half the global physics tick rate.
            if (!isWaitTick)
            {
                if (IsGrenade)
                {
                    direction += Vector3.down * (downwardCurve * tickTime);
                    downwardCurve += grenadeGravity;
                }

                var displacement = (direction * Velocity) * tickTime;
                if (Physics.Raycast(collisionPosition, direction, out var hitInfo, displacement.magnitude + CollisionRadius, playerLayerMask))
                {
                    // Place self at meeting point with collider and self-destruct.
                    collisionPosition = hitInfo.point - direction * CollisionRadius;
                    HandleCollision(hitInfo.collider);
                    return;
                }
                else
                    collisionPosition += displacement;
            }

            // Only process every other physics tick.
            isWaitTick = !isWaitTick;
        }

        private void HandleCollision(Collider other)
        {
            if (impactDetected)
                return;
            // Get entity based on collision type
            DaggerfallEntityBehaviour entityBehaviour;
            if (other != null)
                entityBehaviour = other.gameObject.transform.GetComponent<DaggerfallEntityBehaviour>();
            else
                return;
            if (other != null)
            {
                var shotResult = FutureShockAttack.ShotResult.HitOther;
                if (entityBehaviour)
                    DamageTarget(other, out shotResult);
                isImpactBillboardRequested = IsExplosive || shotResult != FutureShockAttack.ShotResult.HitTarget;
            }

            Destroy(goProjectile);
            impactDetected = true;
            if (IsExplosive)
                DoSplashDamage(collisionPosition);
        }

        void DamageTarget(Collider arrowHitCollider, out FutureShockAttack.ShotResult shotResult)
        {
            shotResult = FutureShockAttack.ShotResult.HitOther;
            // Assumes caster is player for now.
            Transform hitTransform = arrowHitCollider.gameObject.transform;
            if (OriginWeapon != null)
                shotResult = FutureShockAttack.DealDamage(OriginWeapon, hitTransform, hitTransform.position, goProjectile.transform.forward);
        }

        void DoSplashDamage(Vector3 position)
        {
            transform.position = position;
            var overlaps = Physics.OverlapSphere(position, ExplosionRadius);
            foreach (var overlap in overlaps)
            {
                var direction = (overlap.transform.position - position).normalized;
                var ray = new Ray(position, direction);
                if (Physics.Raycast(ray, out RaycastHit hit, ExplosionRadius, playerLayerMask))
                    FutureShockAttack.DealDamage(OriginWeapon, hit.transform, hit.point, ray.direction, true);
            }
        }

        private void PlaySound(AudioClip clip, float spatialBlend, bool loop = false)
        {
            audioSource.clip = clip;
            audioSource.volume = DaggerfallUnity.Settings.SoundVolume;
            audioSource.loop = loop;
            audioSource.dopplerLevel = 0f;
            audioSource.spatialBlend = spatialBlend;
            audioSource.Play();
        }
    }
}
