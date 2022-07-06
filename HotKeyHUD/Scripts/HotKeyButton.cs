using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyButton
    {
        const int iconPanelSlot = 1;
        const int buttonLabelSlot = 2;
        const int buttonConditionBarSlot = 3;
        const float condBarHeight = 1f;
        float maxCondBarWidth;

        public Vector2 Position
        {
            get => Panel.Position;
            set => Panel.Position = value;
        }

        public Vector2 Size
        {
            get => Panel.Size;
            set => Panel.Size = value;
        }

        public bool ForceUse { get; set; }
        public object Payload { get; set; }
        public Panel Panel { get; set; }
        public Panel Icon => (Panel)Panel.Components[iconPanelSlot];
        public TextLabel Label => (TextLabel)Panel.Components[buttonLabelSlot];
        public Panel ConditionBar => (Panel)Panel.Components[buttonConditionBarSlot];

        public HotKeyButton(Texture2D backdrop, Vector2 size, Vector2 position, int keyIndex)
        {
            // Button Backdrop
            Panel = new Panel
            {
                BackgroundColor = Color.black,
                BackgroundTexture = backdrop,
                Size = size,
                Position = position
            };

            // Payload Icon - Note: doesn't always fit vertically despite scaling.
            Panel.Components.Add(new Panel
            {
                BackgroundColor = Color.clear,
                AutoSize = AutoSizeModes.ScaleToFit,
                MaxAutoScale = 1f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle
            });

            // Key # Label
            Panel.Components.Add(new TextLabel
            {
                Position = new Vector2(1f, 1f),
                HorizontalAlignment = HorizontalAlignment.None,
                Text = keyIndex.ToString(),
                TextScale = 2f
            });

            // Item condition bar
            maxCondBarWidth = size.x - 3f;
            Panel.Components.Add(new Panel
            {
                Position = new Vector2(1f, size.y - 4f),
                Size = new Vector2(maxCondBarWidth, condBarHeight),
                BackgroundColor = Color.green,
                Enabled = false
            });
        }

        public void UpdateCondition(int percentage, in Vector2 scale)
        {;
            if (percentage <= 0)
            {
                ConditionBar.Enabled = false;
                return;
            }

            if (!ConditionBar.Enabled)
                ConditionBar.Enabled = true;
            // Shrink bar as value decreases.
            ConditionBar.Size = new Vector2(percentage / 100f * (maxCondBarWidth * scale.x), condBarHeight * scale.y);
            if (percentage >= 75)
                ConditionBar.BackgroundColor = Color.green;
            else if (percentage >= 25)
                ConditionBar.BackgroundColor = Color.yellow;
            else
                ConditionBar.BackgroundColor = Color.red;
        }
    }
}
