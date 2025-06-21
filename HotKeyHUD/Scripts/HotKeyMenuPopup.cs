using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HotKeyMenuPopup : Panel
    {
        public static bool OverrideMenus { get; set; }
        private const float overrideXOffset = 222f;
        private const float overrideYOffset = 9f;
        private const float nonOverrideXOffset = 21f;
        private const float nonOverrideYOffset = 30f;
        private readonly float xOffset;
        private readonly float yOffset;
        private readonly bool clickable;
        private static HotKeyMenuPopup instance;
        public HotKeyButton[] HotKeyButtons { get; private set; }
        public bool Initialized { get; private set; }
        public int SelectedSlot { get; private set; }

        public static HotKeyMenuPopup Instance
        {
            get
            {
                if (instance == null)
                    instance = new HotKeyMenuPopup(!OverrideMenus);
                return instance;
            }
        }

        private HotKeyMenuPopup(bool clickable) : base()
        {
            if (!clickable)
            {
                xOffset = overrideXOffset;
                yOffset = overrideYOffset;
            }
            else
            {
                xOffset = nonOverrideXOffset;
                yOffset = nonOverrideYOffset;
            }

            Enabled = clickable;
            this.clickable = clickable;
        }

        public void Initialize()
        {
            HotKeyButtons = new HotKeyButton[HotKeyUtil.IconCount];
            var itemBackdrops = HotKeyUtil.ItemBackdrops;
            for (var i = 0; i < HotKeyButtons.Length; i++)
            {
                HotKeyButtons[i] = new HotKeyButton(itemBackdrops[i],
                    new Vector2(xOffset + (float)((i % 3) * HotKeyButton.buttonWidth), yOffset + (i / 3) * HotKeyButton.buttonHeight + .5f), i + 1);
                if (clickable)
                    HotKeyButtons[i].OnMouseClick += HotKeyMenuPopup_OnMouseClick;
                Components.Add(HotKeyButtons[i]);
            }

            if (clickable)
                SetSelectedSlot(0);
            Initialized = true;
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
                    Enabled = true;
            }
            // If overriding inventory window, show hotkey popup when hotkey is pressed and hide when released.
            else if (!clickable)
                Enabled = false;
        }

        public void HandleItemSet(object sender, ItemSetEventArgs args)
        {
            if (args.Index == HotKeyUtil.EquippedButtonIndex)
                return;
            if (args.Item is EffectBundleSettings spell)
                HotKeyButtons[args.Index].SetSpell(spell);
            else
                HotKeyButtons[args.Index].SetItem((DaggerfallUnityItem)args.Item);
        }

        private void HotKeyMenuPopup_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            var button = sender as HotKeyButton;
            SetSelectedSlot(button.PositionIndex);
            DaggerfallUI.Instance.PlayOneShot(DaggerfallWorkshop.SoundClips.ButtonClick);
        }
    }
}
