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
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public sealed class FutureShockProjectile : MonoBehaviour
    {
        public const float SphereCastRadius = 0.25f;
        public float MovementSpeed = 25.0f;                     // Speed missile moves through world
        public float ColliderRadius = 0.45f;                    // Radius of missile contact sphere
        public float ExplosionRadius = 5.0f;                    // Radius of area of effect explosion
        public bool EnableLight = true;                         // Show a light with this missile - player can force disable from settings
        public bool EnableShadows = true;                       // Light will cast shadows - player can force disable from settings
        public Color[] PulseColors;                             // Array of colours for pulse cycle, light will lerp from item-to-item and loop back to start - ignored if empty
        public float PulseSpeed = 0f;                           // Time in seconds light will lerp between pulse colours - 0 to disable
        public float FlickerMaxInterval = 0f;                   // Maximum interval for random flicker - 0 to disable
        public int ImpactBillboardFramesPerSecond = 15;         // Speed of contact billboard animation
        public float LifespanInSeconds = 8f;                    // How long missile will persist in world before self-destructing if no target found
        public float PostImpactLifespanInSeconds = 0.6f;        // Time in seconds missile will persist after impact
        public float PostImpactLightMultiplier = 1f;            // Scale of light intensity and range during post-impact lifespan - use 1.0 for no change, 0.0 for lights-out
        public DaggerfallUnityItem OriginWeapon { private get; set; }

        private Vector3 direction;
        private Light myLight;
        private SphereCollider myCollider;
        private AudioSource audioSource;
        private AudioClip impactSound;
        private AudioClip travelSound;
        private bool travelSoundIsLooped;
        private Rigidbody myRigidbody;
        private bool forceDisableSpellLighting;
        private float lifespan = 0f;
        private float postImpactLifespan = 0f;
        private DaggerfallEntityBehaviour caster = null;
        private bool missileReleased = false;
        private bool impactDetected = false;
        private bool impactAssigned = false;
        private float initialRange;
        private float initialIntensity;
        private GameObject goModel = null;
        private EnemySenses enemySenses;
        private Texture2D[] impactFrames;
        private Vector2 impactSize;

        #region Properties

        public bool IsExplosive { get; set; }

        /// <summary>
        /// Gets or sets caster who is origin of missile.
        /// This must be set for all missile target types.
        /// Caster is set automatically from payload when available.
        /// </summary>
        public DaggerfallEntityBehaviour Caster
        {
            get { return caster; }
            set { caster = value; }
        }

        public void SetImpactFrames(Texture2D[] frames, Vector2 size)
        {
            impactFrames = frames;
            impactSize = size;
        }

        public void SetSounds(AudioClip travel, AudioClip impact, bool travelIsLooped)
        {
            travelSound = travel;
            impactSound = impact;
            travelSoundIsLooped = travelIsLooped;
        }

        public Vector3 CustomAimPosition { get; set; }

        public Vector3 CustomAimDirection { get; set; }

        #endregion

        #region Unity

        private void Awake()
        {
            audioSource = transform.GetComponent<AudioSource>();
        }

        private void Start()
        {
            // Setup light and shadows
            myLight = GetComponent<Light>();
            myLight.enabled = EnableLight;
            forceDisableSpellLighting = !DaggerfallUnity.Settings.EnableSpellLighting;
            if (forceDisableSpellLighting) myLight.enabled = false;
            if (!DaggerfallUnity.Settings.EnableSpellShadows) myLight.shadows = LightShadows.None;
            initialRange = myLight.range;
            initialIntensity = myLight.intensity;

            // Setup collider
            myCollider = GetComponent<SphereCollider>();
            myCollider.radius = ColliderRadius;
            myCollider.isTrigger = true;

            // Setup rigidbody
            myRigidbody = GetComponent<Rigidbody>();
            myRigidbody.useGravity = false;
            myRigidbody.isKinematic = true;

            // Setup senses
            if (caster && caster != GameManager.Instance.PlayerEntityBehaviour)
            {
                enemySenses = caster.GetComponent<EnemySenses>();
            }

            // Setup arrow
            // Create and orient 3d arrow
            goModel = GameObjectHelper.CreateDaggerfallMeshGameObject(99800, transform);
            var arrowCollider = goModel.GetComponent<MeshCollider>();
            arrowCollider.sharedMesh = goModel.GetComponent<MeshFilter>().sharedMesh;
            arrowCollider.convex = true;
            arrowCollider.isTrigger = true;

            // Offset up so it comes from same place LOS check is done from
            Vector3 adjust;
            if (caster != GameManager.Instance.PlayerEntityBehaviour)
            {
                CharacterController controller = caster.transform.GetComponent<CharacterController>();
                adjust = caster.transform.forward * 0.6f;
                adjust.y += controller.height / 3;
            }
            else
            {
                // Adjust to fit gun animations. TODO: Refine so it fits all animations better.
                adjust = Vector3.zero;
                adjust.y -= 0.17f;
                if (!GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal)
                    adjust += GameManager.Instance.MainCamera.transform.right * 0.12f;
                else
                    adjust -= GameManager.Instance.MainCamera.transform.right * 0.12f;
            }

            goModel.transform.localPosition = adjust;
            goModel.transform.rotation = Quaternion.LookRotation(GetAimDirection());
            goModel.layer = gameObject.layer;

            // Ignore collision with caster
            if (caster)
            {
                var casterCollider = caster.GetComponent<Collider>();
                Physics.IgnoreCollision(casterCollider, this.GetComponent<Collider>());
                Physics.IgnoreCollision(casterCollider, arrowCollider);
            }

            if (travelSound)
                PlaySound(travelSound, 0.6f, travelSoundIsLooped);
        }

        private void Update()
        {
            if (!missileReleased)
                DoMissile();

            // Handle missile lifespan pre and post-impact
            if (!impactDetected)
            {
                // Transform missile along direction vector
                transform.position += (direction * MovementSpeed) * Time.deltaTime;

                // Update lifespan and self-destruct if expired (e.g. spell fired straight up and will never hit anything)
                lifespan += Time.deltaTime;
                if (lifespan > LifespanInSeconds)
                    Destroy(gameObject);
            }
            else
            {
                // Notify listeners work is done and automatically assign impact
                if (!impactAssigned)
                {
                    if (impactSound)
                        PlaySound(impactSound, 0.9f);
                    impactAssigned = true;
                }

                // Track post impact lifespan and allow impact clip to finish playing
                postImpactLifespan += Time.deltaTime;
                if (postImpactLifespan > PostImpactLifespanInSeconds)
                {
                    myLight.enabled = false;
                    if (audioSource && !audioSource.isPlaying)
                        Destroy(gameObject);
                }
            }

            // Update light
            // Do nothing if light disabled by missile properties or force disabled in user settings
            if (!EnableLight || forceDisableSpellLighting)
                return;

            // Scale post-impact
            if (impactDetected)
            {
                myLight.range = initialRange * PostImpactLightMultiplier;
                myLight.intensity = initialIntensity * PostImpactLightMultiplier;
            }
        }

        #endregion

        #region Collision Handling

        private void OnCollisionEnter(Collision collision)
        {
            DoCollision(collision, null);
        }

        private void OnTriggerEnter(Collider other)
        {
            DoCollision(null, other);
        }

        void DoCollision(Collision collision, Collider other)
        {
            // Missile collision should only happen once
            if (impactDetected)
                return;
            // Get entity based on collision type
            DaggerfallEntityBehaviour entityBehaviour;
            if (collision != null && other == null)
                entityBehaviour = collision.gameObject.transform.GetComponent<DaggerfallEntityBehaviour>();
            else if (collision == null && other != null)
                entityBehaviour = other.gameObject.transform.GetComponent<DaggerfallEntityBehaviour>();
            else
                return;
            if (other != null)
            {
                var shotResult = FutureShockAttack.ShotResult.HitOther;
                if (entityBehaviour)
                    DamageTarget(other, out shotResult);
                if (IsExplosive || shotResult == FutureShockAttack.ShotResult.HitOther)
                {
                    var go = new GameObject("ImpactBillboard");
                    go.transform.position = transform.position;
                    var billboard = go.AddComponent<ImpactBillboard>();
                    billboard.SetFrames(impactFrames, impactSize);
                }
            }

            // Destroy projectile and disable collider.
            Destroy(goModel);
            myCollider.enabled = false;
            impactDetected = true;
            if (IsExplosive)
                DoSplashDamage(transform.position);
        }

        #endregion

        #region Private Methods

        // Missile can hit environment or target at range
        void DoMissile()
        {
            direction = GetAimDirection();
            transform.position = GetAimPosition() + direction * ColliderRadius;
            missileReleased = true;
        }

        // Get missile aim position from player or enemy mobile
        Vector3 GetAimPosition()
        {
            // Aim position from custom source
            if (CustomAimPosition != Vector3.zero)
                return CustomAimPosition;

            // Aim position is from eye level for player or origin for other mobile
            // Player must aim from camera position or it feels out of alignment
            Vector3 aimPosition = caster.transform.position;
            if (caster == GameManager.Instance.PlayerEntityBehaviour)
            {
                aimPosition = GameManager.Instance.MainCamera.transform.position;
            }

            return aimPosition;
        }

        // Get missile aim direction from player or enemy mobile
        Vector3 GetAimDirection()
        {
            // Aim direction from custom source
            if (CustomAimDirection != Vector3.zero)
                return CustomAimDirection;

            // Aim direction should be from camera for player or facing for other mobile
            Vector3 aimDirection = Vector3.zero;
            if (caster == GameManager.Instance.PlayerEntityBehaviour)
            {
                aimDirection = GameManager.Instance.MainCamera.transform.forward;
            }
            else if (enemySenses)
            {
                Vector3 predictedPosition;
                if (DaggerfallUnity.Settings.EnhancedCombatAI)
                    predictedPosition = enemySenses.PredictNextTargetPos(MovementSpeed);
                else
                    predictedPosition = enemySenses.LastKnownTargetPos;

                if (predictedPosition == EnemySenses.ResetPlayerPos)
                    aimDirection = caster.transform.forward;
                else
                    aimDirection = (predictedPosition - caster.transform.position).normalized;
            }

            return aimDirection;
        }

        void DamageTarget(Collider arrowHitCollider, out FutureShockAttack.ShotResult shotResult)
        {
            shotResult = FutureShockAttack.ShotResult.HitOther;
            // Assumes caster is player for now.
            Transform hitTransform = arrowHitCollider.gameObject.transform;
            if (OriginWeapon != null)
                shotResult = FutureShockAttack.DealDamage(OriginWeapon, hitTransform, hitTransform.position, goModel.transform.forward);
        }

        void DoSplashDamage(Vector3 position)
        {
            transform.position = position;
            var overlaps = Physics.OverlapSphere(position, ExplosionRadius);
            foreach (var overlap in overlaps)
                FutureShockAttack.DealDamage(OriginWeapon, overlap.transform, overlap.transform.position, overlap.transform.position - position, true);
            impactDetected = true;
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

        #endregion

    }
}
