using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyButton
    {
        const int iconPanelSlot = 1;
        const int buttonLabelSlot = 2;
        public bool ForceUse { get; set; }
        public object Payload { get; set; }
        public Panel Panel { get; set; }

        public HotKeyButton(Panel parentPanel, Texture2D backdrop, Vector2 size, Vector2 position, int keyIndex)
        {
            // Button Backdrop
            Panel = new Panel
            {
                //Parent = parentPanel,
                BackgroundColor = Color.black,
                BackgroundTexture = backdrop,
                Size = size,
                Position = position
            };

            // Payload Icon - Note: doesn't always fit vertically despite scaling.
            Panel.Components.Add(new Panel
            {
                //Parent = Panel,
                BackgroundColor = Color.clear,
                AutoSize = AutoSizeModes.ScaleToFit,
                MaxAutoScale = 1f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle
            });

            // Key # Label
            Panel.Components.Add(new TextLabel
            {
                //Parent = Panel,
                Position = new Vector2(1f, 1f),
                HorizontalAlignment = HorizontalAlignment.None,
                Text = keyIndex.ToString(),
                TextScale = 2f
            });
        }

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

        public Panel Icon => (Panel)Panel.Components[iconPanelSlot];
        public TextLabel Label => (TextLabel)Panel.Components[buttonLabelSlot];
    }
}
