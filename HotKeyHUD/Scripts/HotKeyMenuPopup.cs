using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyMenuPopup : Panel
    {
        const float xOffset = 222f;
        const float yOffset = 9f;
        HotKeyButton[] hotKeyButtons;
        HotKeyDisplay hotKeyDisplay;
        public bool Initialized { get; private set; }

        public HotKeyMenuPopup() : base()
        {
            Enabled = false;
        }

        public override void Update()
        {
            if (!Enabled)
                return;
            if (!Initialized)
                Initialize();

            base.Update();
            if (Scale != Parent.Scale)
                SetScale(Parent.Scale);
        }

        public void SyncIcons()
        {
            var buttonList = hotKeyDisplay.ButtonList;
            for (var i = 0; i < hotKeyButtons.Length; i++)
            {
                var popupIcon = hotKeyButtons[i].Icon;
                var hotbarIcon = buttonList[i].Icon;
                hotKeyButtons[i].StackLabel.Enabled = buttonList[i].StackLabel.Enabled;
                if (hotKeyButtons[i].StackLabel.Enabled)
                    hotKeyButtons[i].StackLabel.Text = ((DaggerfallUnityItem)buttonList[i].Payload).stackCount.ToString();
                hotKeyButtons[i].ConditionBar.Enabled = buttonList[i].ConditionBar.Enabled;
                if (hotKeyButtons[i].ConditionBar.Enabled || hotKeyButtons[i].StackLabel.Enabled) // Is an item if either is true
                {
                    var item = (DaggerfallUnityItem)buttonList[i].Payload;
                    hotKeyButtons[i].UpdateCondition(item.ConditionPercentage, buttonList[i].Scale);
                    if (item.IsPotion || item.IsLightSource) // These scale weird
                        popupIcon.Size = hotbarIcon.Size * .7f;
                    else
                        popupIcon.Size = hotbarIcon.Size;
                }
                else
                    popupIcon.Size = popupIcon.Parent.Size * .8f;
                popupIcon.BackgroundTexture = hotbarIcon.BackgroundTexture;
            }
        }

        public void SetSelectedSlot(int index)
        {
            if (index < 0 || hotKeyButtons == null)
                return;
            for (var i = 0; i < hotKeyButtons.Length; i++)
            {
                if (i == index)
                    hotKeyButtons[i].KeyLabel.TextColor = Color.white;
                else
                    hotKeyButtons[i].KeyLabel.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
            }
        }

        private void Initialize()
        {
            hotKeyButtons = new HotKeyButton[HotKeyHUD.iconCount];
            var itemBackdrops = HotKeyHUD.ItemBackdrops;
            for (int i = 0; i < hotKeyButtons.Length; i++)
            {
                hotKeyButtons[i] = new HotKeyButton(itemBackdrops[i],
                    new Vector2(xOffset + (float)((i % 3) * HotKeyButton.iconWidth), yOffset + (i / 3) * HotKeyButton.iconHeight + .5f),
                                i + 1, 1f);
                Components.Add(hotKeyButtons[i]);
            }

            hotKeyDisplay = (HotKeyDisplay)DaggerfallUI.Instance.DaggerfallHUD.ParentPanel.Components.FirstOrDefault(x => x.GetType() == typeof(HotKeyDisplay));
            SyncIcons();

            Initialized = true;
        }

        private void SetScale(Vector2 scale)
        {
            Scale = scale;
            for (int i = 0; i < hotKeyButtons.Length; i++)
                hotKeyButtons[i].SetScale(scale);
        }
    }
}
