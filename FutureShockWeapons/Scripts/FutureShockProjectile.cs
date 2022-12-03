using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace FutureShock
{
    // NOTE: This class is more or less a copy-paste job from DaggerfallMissile.cs.
    // TODO: Trim class down only to what is essential.
    [RequireComponent(typeof(Light))]
    [RequireComponent(typeof(AudioSource))]
    public sealed class FutureShockProjectile : MonoBehaviour
    {
        public float MovementSpeed = 42f;
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
        private const float tickTime = 1f / 30f; // 30 ticks/second
        private float tickTimeRemaining = 0f;
        private bool tickRequested = false;

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
            const float downwardCurve = .03f;
            if (tickTimeRemaining <= 0f)
            {
                tickTimeRemaining = tickTime;
                tickRequested = true;
            }

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
                tickTimeRemaining -= frameDeltaTime;
                if (tickRequested)
                {
                    tickRequested = false;
                    if (IsGrenade)
                        direction += Vector3.down * (downwardCurve * tickTime);
                    var displacement = (direction * MovementSpeed) * tickTime;

                    if (Physics.Raycast(goProjectile.transform.position, direction, out var hitInfo, displacement.magnitude + CollisionRadius, playerLayerMask))
                    {
                        // Place self at meeting point with collider and self-destruct.
                        collisionPosition = hitInfo.point - (transform.forward * (CollisionRadius - .05f)); // Adjust slightly back.
                        transform.position = collisionPosition; // Match visual position with collider position.
                        HandleCollision(hitInfo.collider);
                        return;
                    }
                    else
                        collisionPosition += displacement;
                }

                // Transform missile along direction vector
                transform.position += (direction * MovementSpeed) * frameDeltaTime;
                if (IsGrenade)
                    direction += Vector3.down * (downwardCurve * frameDeltaTime);
                lifespan += frameDeltaTime;
                if (lifespan > LifespanInSeconds)
                    Destroy(gameObject);
            }
            else
            {
                if (!impactAssigned)
                {
                    if (impactSound)
                        PlaySound(impactSound, 0.8f);
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

        private void HandleCollision(Collider other)
        {
            // Missile collision should only happen once
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
                // Move back to contact point
                transform.position = other.ClosestPointOnBounds(transform.position - direction * (CollisionRadius * MovementSpeed * Time.deltaTime)) - direction * CollisionRadius;
                if (entityBehaviour)
                    DamageTarget(other, out shotResult);
                if (IsExplosive || shotResult != FutureShockAttack.ShotResult.HitTarget)
                {
                    var go = new GameObject("ImpactBillboard");
                    go.transform.position = transform.position;
                    var billboard = go.AddComponent<FSBillboard>();
                    billboard.SetFrames(impactFrames, impactSize);
                }
            }

            Destroy(goProjectile);
            impactDetected = true;
            if (IsExplosive)
                DoSplashDamage(transform.position);
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
