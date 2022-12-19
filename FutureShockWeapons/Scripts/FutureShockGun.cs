using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace FutureShock
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class FutureShockGun : MonoBehaviour
    {
        private enum FiringType
        {
            Burst,
            Pellets,
            Projectile,
            ProjectileRapid
        }

        private const float nativeScreenWidth = 320f;
        private const float nativeScreenHeight = 200f;
        private const float frameTime = 0.0625f;
        private const float wepRange = 35f;
        private FiringType firingType;
        private GameObject mainCamera;
        private int playerLayerMask;
        private Rect weaponPosition;
        private int currentFrame;
        private float frameTimeRemaining;
        private float lastScreenWidth, lastScreenHeight;
        private AudioSource audioSource;
        public DaggerfallUnityItem PairedItem { private get; set; }
        public Texture2D[] WeaponFrames { private get; set; }
        public Texture2D[] ImpactFrames { private get; set; }
        public Texture2D[] ProjectileFrames { private get; set; }
        public Vector2 ImpactFrameSize { private get; set; }
        public Vector2 ProjectileFrameSize { private get; set; }
        public float HorizontalOffset { private get; set; }
        public float VerticalOffset { private get; set; }
        public float HorizProjAdjust { private get; set; }
        public float VertProjAdjust { private get; set; }
        public float ShotSpread { private get; set; }
        public float ProjVelocity { private get; set; }
        public Color ProjLightColor { private get; set; }
        public Texture2D ProjectileTexture { private get; set; }
        public AudioClip ShootSound { private get; set; }
        public AudioClip EquipSound { private get; set; }
        public AudioClip ImpactSound { private get; set; }
        public AudioClip TravelSound { private get; set; }
        public bool IsTravelSoundLooped { private get; set; }
        public bool IsFiring { get; set; }
        public bool IsUpdateRequested { private get; set; }
        public bool IsHolstered { get; set; }
        public bool IsExplosive { private get; set; }
        public bool IsGrenadeLauncher { private get; set; }
        public int ShotConditionCost { private get; set; }

        public void SetBurst() { firingType = FiringType.Burst; }

        public void SetPellets() { firingType = FiringType.Pellets; }

        public void SetProjectile() { firingType = FiringType.Projectile; }

        public void SetProjectileRapid() { firingType = FiringType.ProjectileRapid; }

        public void ResetAnimation()
        {
            currentFrame = 0;
            frameTimeRemaining = 0;
            IsFiring = false;
        }

        public void PlayEquipSound()
        {
            audioSource.clip = EquipSound;
            audioSource.volume = DaggerfallUnity.Settings.SoundVolume;
            audioSource.Play();
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            IsHolstered = true;
            ResetAnimation();
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
        }

        private void Update()
        {
            if (frameTimeRemaining <= 0f)
            {
                if (IsFiring || currentFrame != 0) // Keep firing until animation is finished.
                {
                    currentFrame = (currentFrame + 1) % WeaponFrames.Length;
                    frameTimeRemaining = frameTime;
                    var isSoundRequested = false;
                    if (firingType == FiringType.Burst || currentFrame == 1)
                    {
                        if (firingType == FiringType.Pellets)
                            FireMultipleRays();
                        else if (firingType == FiringType.Projectile || firingType == FiringType.ProjectileRapid)
                            FireProjectile();
                        else
                            FireSingleRay();
                        PairedItem.LowerCondition(ShotConditionCost);
                        isSoundRequested = currentFrame == 1;
                    }
                    else if (currentFrame == 3 && IsFiring && firingType == FiringType.ProjectileRapid)
                    {
                        FireProjectile();
                        PairedItem.LowerCondition(ShotConditionCost);
                        isSoundRequested = true;
                    }

                    if (isSoundRequested)
                    {
                        audioSource.clip = ShootSound;
                        audioSource.volume = DaggerfallUnity.Settings.SoundVolume;
                        audioSource.Play();
                    }
                }
                else
                    return;
            }
            else
                frameTimeRemaining -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (IsHolstered || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;
            // Update weapon when resolution changes
            var screenRect = DaggerfallUI.Instance.CustomScreenRect ?? new Rect(0, 0, Screen.width, Screen.height);
            if (screenRect.width != lastScreenWidth ||
                screenRect.height != lastScreenHeight ||
                IsUpdateRequested)
            {
                lastScreenWidth = screenRect.width;
                lastScreenHeight = screenRect.height;
                UpdateWeapon();
            }

            GUI.depth = 0;
            if (Event.current.type.Equals(EventType.Repaint) && !IsHolstered)
                DaggerfallUI.DrawTextureWithTexCoords(weaponPosition, WeaponFrames[currentFrame], new Rect(1, 0, 1 /* -1 to mirror (for left hand) */, 1));
        }

        private void UpdateWeapon()
        {
            var screenRect = DaggerfallUI.Instance.CustomScreenRect ?? new Rect(0, 0, Screen.width, Screen.height);
            var weaponScaleX = (float)screenRect.width / nativeScreenWidth;
            var weaponScaleY = (float)screenRect.height / nativeScreenHeight;
            weaponPosition = new Rect(
                screenRect.x + screenRect.width * (1f + HorizontalOffset) - WeaponFrames[currentFrame].width * weaponScaleX,
                screenRect.y + screenRect.height * (1f + VerticalOffset) - WeaponFrames[currentFrame].height * weaponScaleY,
                WeaponFrames[currentFrame].width * weaponScaleX,
                WeaponFrames[currentFrame].height * weaponScaleY);
        }

        private void FireSingleRay()
        {
            var ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward + new Vector3(Random.Range(-ShotSpread, ShotSpread), Random.Range(-ShotSpread, ShotSpread), Random.Range(-ShotSpread, ShotSpread)));
            if (Physics.Raycast(ray, out RaycastHit hit, wepRange, playerLayerMask))
                switch (FutureShockAttack.DealDamage(PairedItem, hit.transform, hit.point, ray.direction))
                {
                    case FutureShockAttack.ShotResult.HitTarget:
                        GameManager.Instance.PlayerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                        break;
                    case FutureShockAttack.ShotResult.HitOther:
                        CreateImpactBillboard(hit.point - ray.direction * .1f);
                        break;
                    default:
                        break;
                }
        }

        private void FireMultipleRays()
        {
            const int rayCount = 6;
            var tallySkill = false;
            for (var i = 0; i < rayCount; i++)
            {
                var ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward + new Vector3(Random.Range(-ShotSpread, ShotSpread), Random.Range(-ShotSpread, ShotSpread), Random.Range(-ShotSpread, ShotSpread)));
                if (Physics.Raycast(ray, out RaycastHit hit, wepRange, playerLayerMask))
                    switch (FutureShockAttack.DealDamage(PairedItem, hit.transform, hit.point, ray.direction))
                    {
                        case FutureShockAttack.ShotResult.HitTarget:
                            tallySkill = true;
                            break;
                        case FutureShockAttack.ShotResult.HitOther:
                            CreateImpactBillboard(hit.point - ray.direction * .1f);
                            break;
                        default:
                            break;
                    }
            }

            if (tallySkill)
                GameManager.Instance.PlayerEntity.TallySkill(DFCareer.Skills.Archery, 1);
        }

        private void FireProjectile()
        {
            var go = new GameObject("FS Projectile")
            {
                layer = LayerMask.NameToLayer("Player")
            };

            var projectile = go.AddComponent<FutureShockProjectile>();
            projectile.transform.parent = GameObjectHelper.GetBestParent();
            projectile.Caster = GameManager.Instance.PlayerEntityBehaviour;
            projectile.SetImpactFrames(ImpactFrames, ImpactFrameSize);
            projectile.SetProjectileFrames(ProjectileFrames, ProjectileFrameSize);
            projectile.ProjectileTexture = ProjectileTexture;
            projectile.SetSounds(TravelSound, ImpactSound, IsTravelSoundLooped);
            projectile.OriginWeapon = PairedItem;
            projectile.IsExplosive = IsExplosive;
            projectile.IsGrenade = IsGrenadeLauncher;
            projectile.HorizontalAdjust = HorizProjAdjust;
            projectile.VerticalAdjust = VertProjAdjust;
            projectile.Velocity = ProjVelocity;
            projectile.LightColor = ProjLightColor;
        }

        private void CreateImpactBillboard(Vector3 point)
        {
            var go = new GameObject("ImpactBillboard");
            go.layer = gameObject.layer;
            go.transform.position = point;
            var billboard = go.AddComponent<FSBillboard>();
            billboard.SetFrames(ImpactFrames, new Vector2(.5f, .5f));
        }
    }
}
