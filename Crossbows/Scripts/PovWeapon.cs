using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility;
using System;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;

namespace Crossbows
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class PovWeapon : MonoBehaviour
    {
        // TODO: make sure enemies react to this weapon when drawn the same way as FPSWeapon.
        private const float nativeScreenWidth = 320f;
        private const float nativeScreenHeight = 200f;
        private const float frameTime = 0.0625f;
        private Rect weaponPosition;
        private Rect rightHanded = new Rect(1, 0, 1, 1);
        private Rect leftHanded = new Rect(1, 0, -1, 1);
        private int currentFrame;
        private float frameTimeRemaining;
        private float lastScreenWidth, lastScreenHeight;
        private AudioSource audioSource;
        private PlayerEntity playerEntity;
        private GameManager gameManager;
        private float cooldownRemaining;
        public DaggerfallUnityItem PairedItem { private get; set; }
        public Texture2D[] WeaponFrames { private get; set; }
        public int LaunchFrame { private get; set; }
        public float HorizontalOffset { private get; set; }
        public float VerticalOffset { private get; set; }
        public bool IsUpdateRequested { private get; set; }
        public AudioClip EquipSound { private get; set; }
        public AudioClip LoadSound { private get; set; }
        public AudioClip ReadySound { private get; set; }
        public AudioClip ShootSound { private get; set; }
        public int ShotConditionCost { private get; set; }
        public bool IsHolstered { get; set; }
        public bool IsFiring { get; set; }
        public float CooldownTimeMultiplier { private get; set; }
        public void PlayEquipSound() => PlaySound(EquipSound);
        public void PlayLoadSound() => PlaySound(LoadSound);

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            IsHolstered = true;
            playerEntity = GameManager.Instance.PlayerEntity;
            gameManager = GameManager.Instance;
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= Time.deltaTime;
                if (cooldownRemaining <= 0f && !IsHolstered)
                    PlaySound(ReadySound);
            }

            // Update firing animation.
            if (frameTimeRemaining <= 0f)
            {
                if ((IsFiring && cooldownRemaining <= 0f) || currentFrame != 0) // Keep playing animation until finished.
                {
                    currentFrame = (currentFrame + 1) % WeaponFrames.Length;
                    frameTimeRemaining = frameTime;
                    if (currentFrame == LaunchFrame)
                    {
                        ShootMissile();
                        PairedItem.LowerCondition(ShotConditionCost);
                        PlaySound(ShootSound);
                    }
                    else if (currentFrame == WeaponFrames.Length - 1 && playerEntity.Items.GetItem(ItemGroups.Weapons, (int)Weapons.Arrow, allowQuestItem: false) != null)
                    {
                        cooldownRemaining = CooldownTimeMultiplier * FormulaHelper.GetBowCooldownTime(playerEntity); // Can't fire again until cooldown ends.
                        PlaySound(LoadSound);
                    }
                }
            }
            else
                frameTimeRemaining -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (IsHolstered || GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress || gameManager.PlayerSpellCasting.IsPlayingAnim)
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
            if ((cooldownRemaining <= 0f || currentFrame != 0) && Event.current.type.Equals(EventType.Repaint))
                DaggerfallUI.DrawTextureWithTexCoords(weaponPosition, WeaponFrames[currentFrame], DaggerfallUnity.Settings.Handedness == 1 ? leftHanded : rightHanded);
        }

        private void UpdateWeapon()
        {
            var screenRect = DaggerfallUI.Instance.CustomScreenRect ?? new Rect(0, 0, Screen.width, Screen.height);
            var weaponScaleX = (float)screenRect.width / nativeScreenWidth;
            var weaponScaleY = (float)screenRect.height / nativeScreenHeight;
            //var horizOffset = DaggerfallUnity.Settings.Handedness == 1 ? -1f - HorizontalOffset + WeaponFrames[currentFrame].width * weaponScaleX / screenRect.width : HorizontalOffset;
            weaponPosition = new Rect(
                screenRect.x/* + screenRect.width * (1f + horizOffset) - WeaponFrames[currentFrame].width * weaponScaleX*/,
                screenRect.y + screenRect.height * (1f + VerticalOffset) - WeaponFrames[currentFrame].height * weaponScaleY,
                WeaponFrames[currentFrame].width * weaponScaleX,
                WeaponFrames[currentFrame].height * weaponScaleY);
            IsUpdateRequested = false;
        }

        private void ShootMissile()
        {
            var go = new GameObject("Crossbow Arrow")
            {
                layer = LayerMask.NameToLayer("Player")
            };

            go.transform.parent = GameObjectHelper.GetBestParent();
            var missile = go.AddComponent<DaggerfallMissile>();
            if (missile)
            {
                SetMissile(missile); // Set up the way the prefab usually does.
                // Undo adjustment so arrow is centered.
                var cameraTransform = GameManager.Instance.MainCamera.transform;
                if (!GameManager.Instance.WeaponManager.ScreenWeapon.FlipHorizontal)
                    missile.CustomAimPosition = cameraTransform.position - cameraTransform.right * 0.15f;
                else
                    missile.CustomAimPosition = cameraTransform.position + cameraTransform.right * 0.15f;
                var gameManager = GameManager.Instance;
                // Remove arrow
                var playerItems = playerEntity.Items;
                var arrow = playerItems.GetItem(ItemGroups.Weapons, (int)Weapons.Arrow, allowQuestItem: false, priorityToConjured: true);
                playerItems.RemoveOne(arrow);
                // Set missile
                missile.Caster = gameManager.PlayerEntityBehaviour;
                missile.TargetType = TargetTypes.SingleTargetAtRange;
                missile.ElementType = ElementTypes.None;
                missile.IsArrow = true;
                missile.IsArrowSummoned = arrow.IsSummoned;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            audioSource.clip = clip;
            audioSource.volume = DaggerfallUnity.Settings.SoundVolume;
            audioSource.Play();
        }

        private void SetMissile(DaggerfallMissile missile)
        {
            missile.IsArrow = true;
            missile.ImpactSound = SoundClips.ArrowHit;
            var audioSource = missile.GetComponent<AudioSource>();
            audioSource.volume = DaggerfallUnity.Settings.SoundVolume;
            var sphereCollider = missile.GetComponent<SphereCollider>();
            sphereCollider.radius = .4f;
            var light = missile.GetComponent<Light>();
            light.intensity = 0f;
            var rigidBody = missile.GetComponent<Rigidbody>();
            rigidBody.isKinematic = true;
            rigidBody.mass = 1f;
            rigidBody.angularDrag = .05f;
            var meshCollider = missile.GetComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.cookingOptions = MeshColliderCookingOptions.UseFastMidphase | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.CookForFasterSimulation;
        }
    }
}
