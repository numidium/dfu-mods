using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
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
        private readonly PlayerEntity playerEntity;
        private static HotKeyMenuPopup instance;
        public HotKeyButton[] HotKeyButtons { get; private set; }
        public bool Initialized { get; private set; }
        public int SelectedSlot { get; private set; }
        public static HotKeyMenuPopup Instance
        {
            get
            {
                if (instance == null)
                    instance = new HotKeyMenuPopup(!HotKeyHUD.OverrideMenus);
                return instance;
            }
        }

        private HotKeyMenuPopup(bool clickable) : base()
        {
            if (clickable)
            {
                xOffset = HotKeySetupWindow.MenuPopupLeft;
                yOffset = HotKeySetupWindow.TopMarginHeight;
            }
            else
            {
                xOffset = defaultXOffset;
                yOffset = defaultYOffset;
            }

            Enabled = clickable;
            this.clickable = clickable;
            playerEntity = GameManager.Instance.PlayerEntity;
        }

        public override void Update()
        {
            if (!Enabled)
                return;
            base.Update();
        }

        public void SetSelectedSlot(int index)
        {
            if (index < 0)
                return;
            for (var i = 0; i < HotKeyButtons.Length; i++)
            {
                if (i == index)
                    HotKeyButtons[i].KeyLabel.TextColor = Color.white;
                else
                    HotKeyButtons[i].KeyLabel.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
            }

            SelectedSlot = index;
        }

        public void HandleSlotSelect(ref int lastSelectedSlot)
        {
            if (!Initialized)
                Initialize();
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
                var slotNum = hotKey - KeyCode.Alpha1;
                if (slotNum != lastSelectedSlot)
                    SetSelectedSlot(slotNum);
                lastSelectedSlot = slotNum;
                if (!clickable && Enabled == false)
                {
                    Enabled = true;
                    ClearRemovedItems();
                }
            }
            // If overriding inventory window, show hotkey popup when hotkey is pressed and hide when released.
            else if (!clickable)
                Enabled = false;
        }

        private void Initialize()
        {
            HotKeyButtons = new HotKeyButton[HotKeyHUD.iconCount];
            var itemBackdrops = HotKeyHUD.ItemBackdrops;
            var displayButtons = HotKeyHUD.HUDDisplay.HotKeyButtons;
            for (int i = 0; i < HotKeyButtons.Length; i++)
            {
                HotKeyButtons[i] = new HotKeyButton(itemBackdrops[i],
                    new Vector2(xOffset + (float)((i % 3) * HotKeyButton.buttonWidth), yOffset + (i / 3) * HotKeyButton.buttonHeight + .5f), i + 1);
                if (clickable)
                    HotKeyButtons[i].OnMouseClick += HotKeyMenuPopup_OnMouseClick;
                Components.Add(HotKeyButtons[i]);
                // Sync with HUD display counterpart.
                if (displayButtons[i].Payload is DaggerfallUnityItem item)
                    HotKeyButtons[i].SetItem(item, displayButtons[i].ForceUse);
                else if (displayButtons[i].Payload is EffectBundleSettings spell)
                    HotKeyButtons[i].SetSpell(spell);
            }

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

        private void ClearRemovedItems()
        {
            var buttonList = HotKeyHUD.HUDDisplay.HotKeyButtons;
            for (var i = 0; i < buttonList.Length; i++)
            {
                if (!(buttonList[i].Payload is DaggerfallUnityItem item))
                    continue;
                if (!playerEntity.Items.Contains(item.UID))
                {
                    HotKeyButtons[i].SetItem(null);
                    buttonList[i].SetItem(null);
                }
            }
        }
    }
}
