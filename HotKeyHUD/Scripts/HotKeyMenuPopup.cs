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
        private HotKeyButton[] popupButtons;
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
            base.Update();
        }

        /// <summary>
        /// Update all buttons in popup to mirror the buttons in main hotkey display.
        /// </summary>
        public void SyncIcons()
        {
            var buttonList = HotKeyHUD.HUDDisplay.HotKeyButtons;
            for (var i = 0; i < popupButtons.Length; i++)
            {
                if (buttonList[i].Payload is DaggerfallUnityItem item)
                {
                    if (popupButtons[i].Payload != null && ((DaggerfallUnityItem)popupButtons[i].Payload).UID == item.UID)
                        continue;
                    popupButtons[i].SetItem(item, buttonList[i].ForceUse);
                }
                else if (buttonList[i].Payload != null)
                {
                    var spell = (EffectBundleSettings)buttonList[i].Payload;
                    if (popupButtons[i].Payload != null && HotKeyHUD.CompareSpells(spell, (EffectBundleSettings)popupButtons[i].Payload))
                        continue;
                    popupButtons[i].SetSpell(in spell);
                }
                else
                    popupButtons[i].SetItem(null);
            }
        }

        public void SetSelectedSlot(int index)
        {
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
            popupButtons = new HotKeyButton[HotKeyHUD.iconCount];
            var itemBackdrops = HotKeyHUD.ItemBackdrops;
            for (int i = 0; i < popupButtons.Length; i++)
            {
                popupButtons[i] = new HotKeyButton(itemBackdrops[i],
                    new Vector2(xOffset + (float)((i % 3) * HotKeyButton.buttonWidth), yOffset + (i / 3) * HotKeyButton.buttonHeight + .5f), i + 1);
                if (clickable)
                    popupButtons[i].OnMouseClick += HotKeyMenuPopup_OnMouseClick;
                Components.Add(popupButtons[i]);
            }

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

        private void ClearRemovedItems()
        {
            var buttonList = HotKeyHUD.HUDDisplay.HotKeyButtons;
            for (var i = 0; i < buttonList.Length; i++)
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
}
