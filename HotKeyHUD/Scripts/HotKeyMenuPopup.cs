using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
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
        private HotKeyButton[] popupButtons;
        private HotKeyDisplay hotKeyDisplay;
        private readonly PlayerEntity playerEntity;
        public bool Initialized { get; private set; }
        public int SelectedSlot { get; private set; }

        public HotKeyMenuPopup(float x = defaultXOffset, float y = defaultYOffset, bool clickable = false) : base()
        {
            xOffset = x;
            yOffset = y;
            Enabled = clickable;
            this.clickable = clickable;
            playerEntity = GameManager.Instance.PlayerEntity;
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
            for (var i = 0; i < popupButtons.Length; i++)
            {
                var popupIcon = popupButtons[i].Icon;
                var hotbarIcon = buttonList[i].Icon;
                popupButtons[i].StackLabel.Enabled = buttonList[i].StackLabel.Enabled;
                if (popupButtons[i].StackLabel.Enabled)
                    popupButtons[i].StackLabel.Text = ((DaggerfallUnityItem)buttonList[i].Payload).stackCount.ToString();
                popupButtons[i].ConditionBar.Enabled = buttonList[i].ConditionBar.Enabled;
                if (popupButtons[i].ConditionBar.Enabled || popupButtons[i].StackLabel.Enabled) // Is an item if either is true
                {
                    var item = (DaggerfallUnityItem)buttonList[i].Payload;
                    popupButtons[i].UpdateCondition(item.ConditionPercentage, buttonList[i].Scale);
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
            if (!Initialized)
                Initialize();
            if (index < 0)
                return;
            for (var i = 0; i < popupButtons.Length; i++)
            {
                if (i == index)
                    popupButtons[i].KeyLabel.TextColor = Color.white;
                else
                    popupButtons[i].KeyLabel.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
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

            if (!clickable)
            {
                if (!Initialized)
                    Initialize();
                if (hotKey >= KeyCode.Alpha1 && hotKey <= KeyCode.Alpha9)
                {
                    var slotNum = hotKey - KeyCode.Alpha1;
                    if (slotNum != lastSelectedSlot)
                        SetSelectedSlot(slotNum);
                    lastSelectedSlot = slotNum;
                    if (Enabled == false)
                    {
                        Enabled = true;
                        // Remove items that have been removed since window was opened.
                        var buttonList = hotKeyDisplay.ButtonList;
                        for (var i = 0; i < buttonList.Count; i++)
                        {
                            if (!(buttonList[i].Payload is DaggerfallUnityItem item))
                                continue;
                            if (!playerEntity.Items.Contains(item.UID))
                            {
                                popupButtons[i].SetItem(null);
                                buttonList[i].SetItem(null);
                            }
                        }
                    }
                }
                else
                    Enabled = false;
            }
        }

        private void Initialize()
        {
            popupButtons = new HotKeyButton[HotKeyHUD.iconCount];
            var itemBackdrops = HotKeyHUD.ItemBackdrops;
            for (int i = 0; i < popupButtons.Length; i++)
            {
                popupButtons[i] = new HotKeyButton(itemBackdrops[i],
                    new Vector2(xOffset + (float)((i % 3) * HotKeyButton.iconWidth), yOffset + (i / 3) * HotKeyButton.iconHeight + .5f), i + 1);
                if (clickable)
                    popupButtons[i].OnMouseClick += HotKeyMenuPopup_OnMouseClick;
                Components.Add(popupButtons[i]);
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
            for (int i = 0; i < popupButtons.Length; i++)
                popupButtons[i].SetScale(scale);
        }
    }
}
