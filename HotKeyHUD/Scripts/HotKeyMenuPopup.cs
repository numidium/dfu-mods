using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeyMenuPopup : Panel
    {
        private const float defaultXOffset = 222f;
        private const float defaultYOffset = 9f;
        private readonly float xOffset;
        private readonly float yOffset;
        private readonly bool clickable;
        private HotKeyButton[] hotKeyButtons;
        private HotKeyDisplay hotKeyDisplay;
        public bool Initialized { get; private set; }
        public int SelectedSlot { get; private set; }

        public HotKeyMenuPopup(bool clickable = false) : base()
        {
            xOffset = defaultXOffset;
            yOffset = defaultYOffset;
            Enabled = clickable;
            this.clickable = clickable;
        }

        public HotKeyMenuPopup(float x, float y, bool clickable = false) : base()
        {
            xOffset = x;
            yOffset = y;
            Enabled = clickable;
            this.clickable = clickable;
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

            SelectedSlot = index;
        }

        public void HandleSlotSelect(ref int lastSelectedSlot)
        {
            // Show hotkey popup when hotkey is pressed and hide when released.
            var hotKey = KeyCode.Alpha1 - 1;
            var input = InputManager.Instance;
            for (var i = 0; i <= (int)KeyCode.Alpha9; i++)
            {
                var key = KeyCode.Alpha1 + i;
                if (input.GetKey(key))
                    hotKey = key;
            }

            if (hotKey >= KeyCode.Alpha1 && hotKey <= KeyCode.Alpha9)
            {
                Enabled = true;
                var slotNum = hotKey - KeyCode.Alpha1;
                if (slotNum != lastSelectedSlot)
                    SetSelectedSlot(slotNum);
                lastSelectedSlot = slotNum;
            }
            else if (clickable == false)
                Enabled = false;
        }

        private void Initialize()
        {
            hotKeyButtons = new HotKeyButton[HotKeyHUD.iconCount];
            var itemBackdrops = HotKeyHUD.ItemBackdrops;
            for (int i = 0; i < hotKeyButtons.Length; i++)
            {
                hotKeyButtons[i] = new HotKeyButton(itemBackdrops[i],
                    new Vector2(xOffset + (float)((i % 3) * HotKeyButton.iconWidth), yOffset + (i / 3) * HotKeyButton.iconHeight + .5f), i + 1);
                if (clickable)
                    hotKeyButtons[i].OnMouseClick += HotKeyMenuPopup_OnMouseClick;
                Components.Add(hotKeyButtons[i]);
            }

            hotKeyDisplay = (HotKeyDisplay)DaggerfallUI.Instance.DaggerfallHUD.ParentPanel.Components.FirstOrDefault(x => x.GetType() == typeof(HotKeyDisplay));
            SyncIcons();
            if (clickable)
                SetSelectedSlot(0);
            Initialized = true;
        }

        private void HotKeyMenuPopup_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            var button = sender as HotKeyButton;
            SetSelectedSlot(button.PositionIndex);
            DaggerfallUI.Instance.PlayOneShot(DaggerfallWorkshop.SoundClips.ButtonClick);
        }

        private void SetScale(Vector2 scale)
        {
            Scale = scale;
            for (int i = 0; i < hotKeyButtons.Length; i++)
                hotKeyButtons[i].SetScale(scale);
        }
    }
}
