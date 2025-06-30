using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using System;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HKHDisplay : Panel
    {
        private const float iconsY = 177f;
        private const float leftX = 70f;
        private const float retroLeftX = 50f;
        private readonly PlayerEntity playerEntity;
        private static HKHDisplay instance;
        private float textTime;
        public bool Initialized { get; set; }
        public HKHButton[] HotKeyButtons { get; private set; }
        public HKHButton EquippedButton { get; private set; }
        public TextLabel NameLabel { get; private set; }
        public HKHUtil.HUDVisibility Visibility { get; set; }
        public bool EquipDelayDisabled { private get; set; }
        public float ScaleMult { private get; set; }
        public static HKHDisplay Instance
        {
            get
            {
                if (instance == null)
                    instance = new HKHDisplay();
                return instance;
            }
        }

        private HKHDisplay() : base()
        {
            Enabled = false;
            playerEntity = GameManager.Instance.PlayerEntity;
            AutoSize = AutoSizeModes.ResizeToFill;
            Size = DaggerfallUI.Instance.DaggerfallHUD.NativePanel.Size;
            ScaleMult = 1f;
        }

        public Func<string, string> Localize { get; set; }

        public override void Update()
        {
            if (!Enabled)
                return;
            if (!Initialized)
                Initialize();

            base.Update();
            var hud = DaggerfallUI.Instance.DaggerfallHUD;
            var factoredScale = hud.NativePanel.LocalScale * ScaleMult;
            if (Scale != factoredScale)
                SetScale(factoredScale);
            if (NameLabel.Enabled)
            {
                textTime -= Time.deltaTime;
                if (textTime <= 0f)
                    NameLabel.Enabled = false;
            }

            // Update button visibility
            if (Visibility == HKHUtil.HUDVisibility.Equipped && !EquippedButton.Enabled)
            {
                EquippedButton.Enabled = true;
                foreach (var button in HotKeyButtons)
                    button.Enabled = false;
            }
            else if (Visibility == HKHUtil.HUDVisibility.Full && EquippedButton.Enabled)
            {
                EquippedButton.Enabled = false;
                foreach (var button in HotKeyButtons)
                    button.Enabled = true;
            }
        }

        public override void Draw()
        {
            if (!Enabled || Visibility == HKHUtil.HUDVisibility.None)
                return;
            base.Draw();
        }

        public void ResetButtons()
        {
            if (!Initialized)
                return;
            var i = 0;
            foreach (var button in HotKeyButtons)
                SetButtonItem(i++, null);
            EquippedButton.SetItem(null);
        }

        public void ResetItemsHandler()
        {
            ResetButtons();
        }

        public void HandleItemSet(HKHUtil.ItemSetEventArgs args)
        {
            if (args.Item is EffectBundleSettings spell)
                SetButtonSpell(args.Index, spell);
            else
                SetButtonItem(args.Index, (DaggerfallUnityItem)args.Item);
        }

        public void HandleItemActivate(HKHUtil.ItemUseEventArgs args)
        {
            const string emptyKeyText = "NullSlot";
            const float textTimeout = 2.5f;
            textTime = textTimeout;
            NameLabel.Enabled = true;
            if (args.Item == null)
            {
                NameLabel.Text = Localize(emptyKeyText);
                return;
            }

            if (args.Item is EffectBundleSettings spell)
            {
                HotKeyButtons[args.Index].HandleButtonActivate(true);
                NameLabel.Text = spell.Name;
            }
            else if (args.Item is DaggerfallUnityItem dfuItem)
            {
                var racialOverride = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();
                if (racialOverride != null && racialOverride.GetSuppressInventory(out _))
                    return;
                HotKeyButtons[args.Index].HandleButtonActivate();
                NameLabel.Text = dfuItem.LongName;
            }
        }

        public void HandleEquipDelay()
        {
            var weaponManager = GameManager.Instance.WeaponManager;
            // Show "equipping" message if a delay was added.
            ShowEquipDelayMessage(weaponManager.EquipCountdownRightHand, EquipSlots.RightHand);
            ShowEquipDelayMessage(weaponManager.EquipCountdownLeftHand, EquipSlots.LeftHand);
        }

        public void UpdateItemDisplay(int index, DaggerfallUnityItem dfuItem)
        {
            if (index == HKHUtil.EquippedButtonIndex)
                EquippedButton.UpdateItemDisplay(dfuItem);
            else
                HotKeyButtons[index].UpdateItemDisplay(dfuItem);
        }

        /// <summary>
        /// Set button's item.
        /// </summary>
        /// <param name="index">Button index.</param>
        /// <param name="item">Item to be assigned to button.</param>
        /// <param name="forceUse">Whether or not to "Use" the item on activation.</param>
        private void SetButtonItem(int index, DaggerfallUnityItem item)
        {
            if (index == HKHUtil.EquippedButtonIndex)
            {
                EquippedButton.SetItem(item);
                return;
            }

            HotKeyButtons[index].SetItem(item);
        }

        private void SetButtonSpell(int index, EffectBundleSettings spell)
        {
            HotKeyButtons[index].SetSpell(spell);
        }

        private void Initialize()
        {
            // Init buttons/icons.
            Components.Clear();
            HotKeyButtons = new HKHButton[HKHUtil.IconCount];
            float xPosition = 0f;
            var itemBackdrops = HKHUtil.ItemBackdrops;
            for (var i = 0; i < HotKeyButtons.Length; i++)
            {
                var position = new Vector2 { x = xPosition, y = iconsY };
                HotKeyButtons[i] = new HKHButton(itemBackdrops[i], position, i + 1);
                Components.Add(HotKeyButtons[i]);
                xPosition += HKHButton.buttonWidth;
            }

            EquippedButton = new HKHButton(itemBackdrops[0], new Vector2 { x = 0f, y = iconsY }, 0);
            EquippedButton.KeyLabel.Enabled = false;
            EquippedButton.Enabled = false;
            Components.Add(EquippedButton);
            var hudNativePanel = DaggerfallUI.Instance.DaggerfallHUD.NativePanel;
            var localScale = hudNativePanel.LocalScale;
            NameLabel = new TextLabel
            {
                Scale = localScale,
                HorizontalAlignment = HorizontalAlignment.None,
                VerticalAlignment = VerticalAlignment.None,
                Enabled = false,
                Text = string.Empty,
                ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor,
                ShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos
            };

            Components.Add(NameLabel);
            SetScale(hudNativePanel.LocalScale);
            Initialized = true;
        }

        private void SetScale(Vector2 scale)
        {
            Scale = scale;
            Position = new Vector2((Size.x - Size.x * ScaleMult) / 2f, Size.y - Size.y * ScaleMult);
            for (var i = 0; i < HotKeyButtons.Length; i++)
                HotKeyButtons[i].SetScale(scale);
            EquippedButton.SetScale(scale);
            NameLabel.Scale = scale;
            NameLabel.TextScale = scale.x;
            NameLabel.Position = new Vector2(DaggerfallUnity.Settings.RetroModeAspectCorrection != (int)RetroModeAspects.Off && DaggerfallUnity.Settings.RetroRenderingMode != 0 ? retroLeftX * scale.x : leftX * scale.x, (iconsY - 7f) * scale.y);
        }

        private void ShowEquipDelayMessage(float countDownValue, EquipSlots equipSlot)
        {
            if (countDownValue > 0)
            {
                var currentWeapon = playerEntity.ItemEquipTable.GetItem(equipSlot);
                if (currentWeapon != null)
                {
                    var message = TextManager.Instance.GetLocalizedText("equippingWeapon");
                    message = message.Replace("%s", currentWeapon.ItemTemplate.name);
                    DaggerfallUI.Instance.PopupMessage(message);
                }
            }
        }
    }
}
