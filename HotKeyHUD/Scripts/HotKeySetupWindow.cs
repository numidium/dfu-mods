using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeySetupWindow : DaggerfallPopupWindow
    {
        private const float topMarginHeight = 30f;
        private const float paddingWidth = 21f;
        private static Rect itemListScrollerRect = new Rect(21f, topMarginHeight, 59f, 152f);
        private static Rect spellsListRect = new Rect(itemListScrollerRect.x + itemListScrollerRect.width + paddingWidth, topMarginHeight, 110f, 130f);
        private static Rect menuPopupRect = new Rect(spellsListRect.x + spellsListRect.width + paddingWidth, topMarginHeight, HotKeyButton.iconWidth * 3f, HotKeyButton.iconHeight * 3f);
        private static Rect spellsListCutoutRect = new Rect(0f, 0f, 120f, 147f);
        private static Rect exitButtonCutoutRect = new Rect(216f, 149f, 43f, 15f);
        private HotKeyMenuPopup hotkeyMenuPopup;
        private ItemListScroller itemListScroller;
        private ListBox spellsList;
        private int lastSelectedSlot = -1;
        private const string spellBookTextureFilename = "SPBK00I0.IMG";
        private Panel spellsListPanel;
        private Panel exitButtonPanel;

        public HotKeySetupWindow(IUserInterfaceManager uiManager) : base(uiManager)
        {
        }

        public override void OnPush()
        {
            base.OnPush();
            if (IsSetup)
            {
                ResetItemsList();
                ResetSpellsList();
            }
        }

        public override void Update()
        {
            base.Update();
            hotkeyMenuPopup.HandleSlotSelect(ref lastSelectedSlot);
            // Toggle closed
            var keyDown = InputManager.Instance.GetAnyKeyDown();
            if (keyDown == HotKeyHUD.SetupMenuKey)
                CloseWindow();
        }

        protected override void Setup()
        {
            if (IsSetup)
                return;
            base.Setup();
            itemListScroller = new ItemListScroller(defaultToolTip)
            {
                Position = new Vector2(itemListScrollerRect.x, itemListScrollerRect.y),
                Size = new Vector2(itemListScrollerRect.width, itemListScrollerRect.height)
            };

            var spellbookTexture = ImageReader.GetTexture(spellBookTextureFilename);
            spellsListPanel = new Panel()
            {
                Position = new Vector2(spellsListRect.x, spellsListRect.y),
                Size = new Vector2(spellsListRect.width, spellsListRect.height),
                BackgroundTexture = ImageReader.GetSubTexture(spellbookTexture, spellsListCutoutRect)
            };

            spellsList = new ListBox()
            {
                Position = new Vector2(6f, 13f),
                Size = new Vector2(spellsListRect.width - 13f, spellsListRect.height - 6f),
            };

            exitButtonPanel = new Panel()
            {
                Position = new Vector2(277f, 185f),
                Size = new Vector2(exitButtonCutoutRect.width, exitButtonCutoutRect.height),
                BackgroundTexture = ImageReader.GetSubTexture(spellbookTexture, exitButtonCutoutRect)
            };

            hotkeyMenuPopup = new HotKeyMenuPopup(menuPopupRect.x, menuPopupRect.y, true);
            ResetItemsList();
            ResetSpellsList();
            ParentPanel.BackgroundColor = Color.clear;
            spellsListPanel.Components.Add(spellsList);
            NativePanel.Components.Add(itemListScroller);
            NativePanel.Components.Add(spellsListPanel);
            NativePanel.Components.Add(hotkeyMenuPopup);
            NativePanel.Components.Add(exitButtonPanel);
            NativePanel.Components.Add(new TextLabel()
            {
                Position = new Vector2(0f, 3f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = "Hot Key Assignment"
            });
            var exitButton = DaggerfallUI.AddButton(new Rect(exitButtonPanel.Position.x, exitButtonPanel.Position.y,
                exitButtonCutoutRect.width, exitButtonCutoutRect.height), NativePanel);
            exitButton.OnMouseClick += ExitButton_OnMouseClick;
            exitButton.ClickSound = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            IsSetup = true;
        }

        private void ExitButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            CloseWindow();
        }

        private void ResetItemsList()
        {
            var itemCollection = GameManager.Instance.PlayerEntity.Items;
            var items = new List<DaggerfallUnityItem>();
            for (var i = 0; i < itemCollection.Count; i++)
                items.Add(itemCollection.GetItem(i));
            itemListScroller.Items = items;
        }

        private void ResetSpellsList()
        {
            spellsList.ClearItems();
            var spellbook = GameManager.Instance.PlayerEntity.GetSpells();
            foreach (var spell in spellbook)
                spellsList.AddItem(spell.Name);
        }
    }
}
