using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using System;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HKHButton : Panel
    {
        private enum ComponentSlot
        {
            IconPanel = 1,
            ButtonKeyLabel,
            ButtonConditionBarBackground,
            ButtonConditionBar,
            ButtonStackLabel
        }

        public const float buttonWidth = 22f;
        public const float buttonHeight = 22f;
        private const float maxCondBarWidth = buttonWidth - 3f;
        private const float condBarHeight = 1f;
        private const float iconsWidth = buttonWidth * 10f;
        private const float iconsY = 177f;
        private const float buttonXStart = 180f;
        private const float retroButtonXStart = 160f;
        const float spellIconScale = .8f;
        private readonly Vector2 originalPosition;
        public byte PositionIndex { get; set; }
        public Panel Icon => (Panel)Components[(int)ComponentSlot.IconPanel];
        public TextLabel KeyLabel => (TextLabel)Components[(int)ComponentSlot.ButtonKeyLabel];
        public TextLabel StackLabel => (TextLabel)Components[(int)ComponentSlot.ButtonStackLabel];
        public Panel ConditionBarBackground => (Panel)Components[(int)ComponentSlot.ButtonConditionBarBackground];
        public Panel ConditionBar => (Panel)Components[(int)ComponentSlot.ButtonConditionBar];
        private Vector2 itemBgSize, itemBgTexSize;
        private static Vector2 KeyLabelOriginalPos = new Vector2(1f, 1f);
        private static Vector2 StackLabelOriginalPos = new Vector2(1f, 14f);
        private static Vector2 CondBarOriginalPos = new Vector2(2f, buttonHeight - 3f);

        public HKHButton(Texture2D backdrop, Vector2 position, int keyIndex)
        {
            // Button Backdrop
            BackgroundColor = Color.black;
            BackgroundTexture = backdrop;
            Size = new Vector2 { x = buttonWidth + 1f, // Scaling workaround (width > height)
                                 y = buttonHeight };
            Position = position;
            originalPosition = position;

            // Payload Icon
            Components.Add(new Panel
            {
                BackgroundColor = Color.clear,
                AutoSize = AutoSizeModes.ScaleToFit,
                MaxAutoScale = 1f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle
            });

            // Key # Label
            Components.Add(new TextLabel
            {
                Position = KeyLabelOriginalPos,
                HorizontalAlignment = HorizontalAlignment.None,
                Text = keyIndex.ToString(),
                ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor,
                ShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos
            });

            // Item condition bar background
            Components.Add(new Panel
            {
                Position = CondBarOriginalPos,
                Size = new Vector2(maxCondBarWidth, condBarHeight),
                BackgroundColor = Color.black,
                Enabled = false
            });

            // Item condition bar
            Components.Add(new Panel
            {
                Position = CondBarOriginalPos,
                Size = new Vector2(maxCondBarWidth, condBarHeight),
                BackgroundColor = Color.green,
                Enabled = false
            });

            // Stack # Label
            Components.Add(new TextLabel
            {
                Position = StackLabelOriginalPos,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.None,
                Text = string.Empty,
                Enabled = false,
                ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor,
                ShadowPosition = DaggerfallUI.DaggerfallDefaultShadowPos
            });

            PositionIndex = (byte)(keyIndex - 1);
        }

        public void UpdateItemDisplay(DaggerfallUnityItem dfuItem)
        {
            // Update stack count.
            if (StackLabel.Enabled)
                StackLabel.Text = HKHUtil.IsBow(dfuItem) ? GetArrowCount().ToString() : dfuItem.stackCount.ToString();
            // Update condition bar.
            ConditionBar.Size = new Vector2(dfuItem.ConditionPercentage / 100f * (maxCondBarWidth * Parent.Scale.x), condBarHeight * Parent.Scale.y);
            if (dfuItem.ConditionPercentage >= 70)
                ConditionBar.BackgroundColor = Color.green;
            else if (dfuItem.ConditionPercentage >= 20)
                ConditionBar.BackgroundColor = Color.yellow;
            else
                ConditionBar.BackgroundColor = Color.red;
        }

        public void SetItem(DaggerfallUnityItem item)
        {
            if (item == null)
            {
                Icon.BackgroundTexture = null;
                ConditionBarBackground.Enabled = false;
                ConditionBar.Enabled = false;
                StackLabel.Enabled = false;
            }
            else
            {
                var image = DaggerfallUnity.Instance.ItemHelper.GetInventoryImage(item);
                itemBgSize = new Vector2(image.width, image.height);
                itemBgTexSize = new Vector2(image.texture.width, image.texture.height);
                Icon.BackgroundTexture = image.texture;
                Icon.Size = new Vector2(image.width == 0 ? itemBgTexSize.x : itemBgSize.x, itemBgSize.y == 0 ? itemBgTexSize.y : itemBgSize.y);
                StackLabel.Enabled = item.IsStackable() || HKHUtil.IsBow(item);
                // I'm assuming there aren't any stackables with condition worth tracking.
                ConditionBar.Enabled = !StackLabel.Enabled || HKHUtil.IsBow(item);
                ConditionBarBackground.Enabled = ConditionBar.Enabled;
            }
        }

        public void SetSpell(in EffectBundleSettings spell)
        {
            Icon.BackgroundTexture = DaggerfallUI.Instance.SpellIconCollection.GetSpellIcon(spell.Icon);
            Icon.Size = new Vector2(Icon.Parent.Size.x * spellIconScale, Icon.Parent.Size.y * spellIconScale);
            StackLabel.Enabled = false;
            ConditionBarBackground.Enabled = false;
            ConditionBar.Enabled = false;
        }

        // TODO: show visual effects on buttons when they are activated.
        public void HandleButtonActivate(bool isSpell = false)
        {

        }

        public void SetScale(Vector2 scale)
        {
            var xStart = DaggerfallUnity.Settings.RetroModeAspectCorrection != (int)RetroModeAspects.Off && DaggerfallUnity.Settings.RetroRenderingMode != 0 ? retroButtonXStart : buttonXStart;
            Scale = scale;
            Position = new Vector2((float)Math.Round((xStart - iconsWidth / 2f + originalPosition.x + 0.5f) * scale.x) + .5f, (float)Math.Round(iconsY * scale.y) + .5f);
            Size = new Vector2((float)Math.Round(buttonWidth * scale.x + .5f), (float)Math.Round(buttonHeight * scale.y) + .5f);
            if (ConditionBar.Enabled)
                Icon.Size = new Vector2(itemBgSize.x == 0 ? itemBgTexSize.x : itemBgSize.x, itemBgSize.y == 0 ? itemBgTexSize.y : itemBgSize.y);
            else
                Icon.Size = new Vector2(Icon.Parent.Size.x * spellIconScale, Icon.Parent.Size.y * spellIconScale);
            KeyLabel.Scale = scale;
            KeyLabel.Position = new Vector2((float)Math.Round(KeyLabelOriginalPos.x * scale.x + .5f), (float)Math.Round(KeyLabelOriginalPos.y * scale.y + .5f));
            KeyLabel.TextScale = scale.x;
            StackLabel.Scale = scale;
            StackLabel.Position = new Vector2((float)Math.Round(StackLabelOriginalPos.x * scale.x + .5f), (float)Math.Round(StackLabelOriginalPos.y * scale.y + .5f));
            StackLabel.TextScale = scale.x;
            ConditionBarBackground.Scale = scale;
            ConditionBarBackground.Position = new Vector2((float)Math.Round(CondBarOriginalPos.x * scale.x + .5f), (float)Math.Round(CondBarOriginalPos.y * scale.y + .5f));
            ConditionBarBackground.Size = new Vector2((float)Math.Round(maxCondBarWidth * scale.x + .5f), (float)Math.Round(condBarHeight * scale.y + .5f));
            ConditionBar.Scale = scale;
            ConditionBar.Position = new Vector2((float)Math.Round(CondBarOriginalPos.x * scale.x + .5f), (float)Math.Round(CondBarOriginalPos.y * scale.y + .5f));
            ConditionBar.Size = new Vector2((float)Math.Round(ConditionBar.Size.x * scale.x + .5f), (float)Math.Round(ConditionBar.Size.y * scale.y + .5f));
        }

        private static int GetArrowCount()
        {
            var arrows = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.Weapons, (int)Weapons.Arrow, allowQuestItem: false, priorityToConjured: true);
            if (arrows != null)
                return arrows.stackCount;
            return 0;
        }
    }
}
