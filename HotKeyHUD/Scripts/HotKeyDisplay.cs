using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyDisplay : Panel
    {
        const float iconsSize = 10f;
        const float iconsWidth = iconsSize * 10f;
        const float iconsY = 185f;

        Panel[] itemIcons;

        public HotKeyDisplay() : base()
        {
            Enabled = false;
            InitIcons();
        }

        public override void Update()
        {
            if (!Enabled)
                return;
            base.Update();
            var gameManager = GameManager.Instance;
            var hud = DaggerfallUI.Instance.DaggerfallHUD;
            SetScale(hud.HUDCompass.Scale); // Compass is an arbitrary choice to get scale. Doesn't matter which HUD element is used.
            Enabled = gameManager.IsPlayingGame() && hud.Enabled; // Only stay visible when the normal HUD is.
        }

        public override void Draw()
        {
            if (!Enabled)
                return;
            base.Draw();
            // Draw key numbers for icons
            foreach (var icon in itemIcons)
            {
                DaggerfallUI.DefaultFont.DrawText(icon.Tag.ToString(), new Vector2(icon.Position.x + 3f, icon.Position.y + 3f), new Vector2(1.5f, 1.5f), Color.white);
            }
        }

        private void InitIcons()
        {
            Components.Clear();
            itemIcons = new Panel[10];
            float xPosition = 0f;
            var hud = DaggerfallUI.Instance.DaggerfallHUD;
            for (int i = 0; i < itemIcons.Length; i++)
            {
                itemIcons[i] = new Panel
                {
                    Position = new Vector2 { x = xPosition, y = iconsY },
                    Size = new Vector2 { x = iconsSize, y = iconsSize },
                    BackgroundColor = Color.black,
                    Parent = hud.ParentPanel,
                    Tag = (i + 1) % 10 // 1, 2, ... 9, 0
                };

                itemIcons[i].Outline.Color = Color.white;
                itemIcons[i].Outline.Enabled = true;
                xPosition += iconsSize;
                Components.Add(itemIcons[i]);
            }
        }

        private void SetScale(Vector2 scale)
        {
            if (Scale.Equals(scale))
                return;
            Scale = scale;
            foreach (var icon in itemIcons)
            {
                // Scale icons to screen and center
                icon.Position = new Vector2((160f - iconsWidth / 2f + icon.Position.x) * scale.x, iconsY * scale.y);
                icon.Size = new Vector2(iconsSize * scale.x, iconsSize * scale.y);
            }
        }
    }
}
