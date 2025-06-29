using DaggerfallConnect.Arena2;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;

namespace HotKeyHUD
{
    public sealed class HKHSetupWindow : DaggerfallPopupWindow
    {
        private const float paddingWidth = 21f;
        private const float magicAnimationDelay = 0.15f;
        private const float topMarginHeight = 30f;
        private const float menuPopupLeft = 21f;
        private const string magicAnimTextureName = "TEXTURE.434";
        private const string spellBookTextureFilename = "SPBK00I0.IMG";
        private readonly Rect menuPopupRect;
        private readonly Rect itemListScrollerRect;
        private readonly Rect spellsListCutoutRect;
        private readonly Rect spellsListRect;
        private readonly Rect spellsListScrollBarRect;
        private readonly Rect exitButtonCutoutRect;
        private HKHMenuPopup hotKeyMenuPopup;
        private ItemListScroller itemListScroller;
        private VerticalScrollBar spellsListScrollBar;
        private ListBox spellsList;
        private ImageData magicAnimation;
        private Panel spellsListPanel;
        private Panel spellsListScrollBarPanel;
        private Panel exitButtonPanel;
        private int lastSelectedSlot = -1;
        private static HKHSetupWindow hotKeySetupWindow;
        public static HKHSetupWindow Instance
        {
            get
            {
                if (hotKeySetupWindow == null)
                    hotKeySetupWindow = new HKHSetupWindow(DaggerfallUI.Instance.UserInterfaceManager);
                return hotKeySetupWindow;
            }
        }

        public EventHandler<HKHUtil.KeyItemEventArgs> OnKeyItem;

        private HKHSetupWindow(IUserInterfaceManager uiManager) : base(uiManager)
        {
            menuPopupRect = new Rect(menuPopupLeft, topMarginHeight, HKHButton.buttonWidth * 3f, HKHButton.buttonHeight * 3f);
            itemListScrollerRect = new Rect(menuPopupRect.x + menuPopupRect.width + paddingWidth, topMarginHeight, 59f, 152f);
            spellsListCutoutRect = new Rect(0f, 0f, 120f, 147f);
            spellsListRect = new Rect(itemListScrollerRect.x + itemListScrollerRect.width + paddingWidth, topMarginHeight, spellsListCutoutRect.width, spellsListCutoutRect.height);
            spellsListScrollBarRect = new Rect(spellsListRect.x + spellsListRect.width + 1f, topMarginHeight + 27f, 8f, spellsListCutoutRect.height - 43f);
            exitButtonCutoutRect = new Rect(216f, 149f, 43f, 15f);
        }

        public Func<string, string> Localize { get; set; }

        public override void OnPush()
        {
            base.OnPush();
            if (IsSetup)
            {
                ResetItemsList();
                ResetSpellsList();
                UpdateSpellScroller();
            }
        }

        public override void Update()
        {
            base.Update();
            hotKeyMenuPopup.HandleSlotSelect(ref lastSelectedSlot);
        }

        protected override void Setup()
        {
            if (IsSetup)
                return;
            base.Setup();
            magicAnimation = ImageReader.GetImageData(magicAnimTextureName, 5, 0, true, false, true);
            itemListScroller = new ItemListScroller(defaultToolTip)
            {
                Position = new Vector2(itemListScrollerRect.x, itemListScrollerRect.y),
                Size = new Vector2(itemListScrollerRect.width, itemListScrollerRect.height),
                ForegroundAnimationHandler = MagicItemForegroundAnimationHander,
                ForegroundAnimationDelay = magicAnimationDelay
            };

            itemListScroller.OnItemClick += ItemListScroller_OnItemClick;
            var spellbookTexture = GetTextureFromVanillaImg(spellBookTextureFilename); // Bypass texture replacements.
            spellsListPanel = new Panel()
            {
                Position = new Vector2(spellsListRect.x, spellsListRect.y),
                Size = new Vector2(spellsListCutoutRect.width, spellsListCutoutRect.height),
                BackgroundTexture = ImageReader.GetSubTexture(spellbookTexture, spellsListCutoutRect)
            };
            
            spellsList = new ListBox()
            {
                Position = new Vector2(6f, 13f),
                Size = new Vector2(spellsListRect.width - 13f, spellsListRect.height - 6f),
                RowsDisplayed = 16,
                MaxCharacters = 22
            };

            spellsList.OnSelectItem += SpellsList_OnSelectItem;
            spellsList.OnMouseScrollDown += SpellsListBox_OnMouseScroll;
            spellsList.OnMouseScrollUp += SpellsListBox_OnMouseScroll;

            spellsListScrollBarPanel = new Panel()
            {
                Position = new Vector2(spellsListScrollBarRect.x - 1f, spellsListScrollBarRect.y - 16f),
                Size = new Vector2(8f, 137f),
                BackgroundTexture = ImageReader.GetSubTexture(spellbookTexture, new Rect(spellsListCutoutRect.x + 121f, 11f, 8f, 137f))
            };

            spellsListScrollBar = new VerticalScrollBar()
            {
                HorizontalAlignment = HorizontalAlignment.None,
                VerticalAlignment = VerticalAlignment.None,
                Position = new Vector2(spellsListScrollBarRect.x, spellsListScrollBarRect.y),
                Size = new Vector2(spellsListScrollBarRect.width, spellsListScrollBarRect.height),
                TotalUnits = spellsList.Count,
                DisplayUnits = spellsList.RowsDisplayed,
                ScrollIndex = 0
            };

            spellsListScrollBar.OnScroll += SpellsListScrollBar_OnScroll;
            exitButtonPanel = new Panel()
            {
                Position = new Vector2(277f, 185f),
                Size = new Vector2(exitButtonCutoutRect.width, exitButtonCutoutRect.height),
                BackgroundTexture = ImageReader.GetSubTexture(spellbookTexture, exitButtonCutoutRect)
            };

            hotKeyMenuPopup = HKHMenuPopup.Instance;
            ResetItemsList();
            ResetSpellsList();
            ParentPanel.BackgroundColor = Color.clear;
            spellsListPanel.Components.Add(spellsList);
            NativePanel.Components.Add(itemListScroller);
            NativePanel.Components.Add(spellsListPanel);
            NativePanel.Components.Add(spellsListScrollBarPanel);
            NativePanel.Components.Add(spellsListScrollBar);
            NativePanel.Components.Add(hotKeyMenuPopup);
            NativePanel.Components.Add(exitButtonPanel);
            NativePanel.Components.Add(new TextLabel()
            {
                Position = new Vector2(0f, 3f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = Localize("SetupTitle")
            });
            var exitButton = DaggerfallUI.AddButton(new Rect(exitButtonPanel.Position.x, exitButtonPanel.Position.y,
                exitButtonCutoutRect.width, exitButtonCutoutRect.height), NativePanel);
            exitButton.OnMouseClick += ExitButton_OnMouseClick;
            exitButton.ClickSound = DaggerfallUI.Instance.GetAudioClip(SoundClips.ButtonClick);
            UpdateSpellScroller();
            if (!hotKeyMenuPopup.Initialized)
                hotKeyMenuPopup.Initialize();
            IsSetup = true;
        }

        private void SpellsListScrollBar_OnScroll()
        {
            spellsList.ScrollIndex = spellsListScrollBar.ScrollIndex;
        }

        private void SpellsListBox_OnMouseScroll(BaseScreenComponent sender)
        {
            spellsListScrollBar.ScrollIndex = spellsList.ScrollIndex;
        }

        private void ExitButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            CloseWindow();
        }

        private void ItemListScroller_OnItemClick(DaggerfallUnityItem item)
        {
            const int forbiddenEquipmentTextId = 1068;
            const int itemBrokenTextId = 29;
            if (HKHUtil.GetProhibited(item))
            {
                PushMessageBox(forbiddenEquipmentTextId);
                return;
            }
            else if (item.currentCondition < 1)
            {
                PushMessageBox(itemBrokenTextId, item);
                return;
            }

            RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(item, hotKeyMenuPopup.SelectedSlot, PreviousWindow, hotKeyMenuPopup));
        }

        private void SpellsList_OnSelectItem()
        {
            var spellBook = GameManager.Instance.PlayerEntity.GetSpells();
            var spell = spellBook[spellsList.SelectedIndex];
            RaiseKeyItemEvent(new HKHUtil.KeyItemEventArgs(spell, hotKeyMenuPopup.SelectedSlot, PreviousWindow, hotKeyMenuPopup));
            UpdateSpellScroller();
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
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

        private Texture2D[] MagicItemForegroundAnimationHander(DaggerfallUnityItem item) => item.IsEnchanted ? magicAnimation.animatedTextures : null;

        private void UpdateSpellScroller()
        {
            spellsListScrollBar.Reset(spellsList.RowsDisplayed, spellsList.Count, spellsList.ScrollIndex);
            spellsListScrollBar.TotalUnits = spellsList.Count;
            spellsListScrollBar.ScrollIndex = spellsList.ScrollIndex;
        }

        private Texture2D GetTextureFromVanillaImg(string fileName)
        {
            var dfUnity = DaggerfallUnity.Instance;
            var imgFile = new ImgFile(Path.Combine(dfUnity.Arena2Path, fileName), FileUsage.UseMemory, true);
            imgFile.LoadPalette(Path.Combine(dfUnity.Arena2Path, imgFile.PaletteName));
            var dfBitmap = imgFile.GetDFBitmap();
            return ImageReader.GetTexture(dfBitmap.GetColor32(), dfBitmap.Width, dfBitmap.Height);
        }

        private void PushMessageBox(int tokenId, IMacroContextProvider macroContextProvider = null)
        {
            var tokens = DaggerfallUnity.Instance.TextProvider.GetRSCTokens(tokenId);
            if (tokens != null && tokens.Length > 0)
            {
                var messageBox = new DaggerfallMessageBox(uiManager, this);
                messageBox.SetTextTokens(tokens, macroContextProvider);
                messageBox.ClickAnywhereToClose = true;
                messageBox.Show();
            }
        }

        private void RaiseKeyItemEvent(HKHUtil.KeyItemEventArgs args)
        {
            OnKeyItem?.Invoke(this, args);
        }

        public void HandleKeyDown(KeyCode key)
        {

        }

        public void HandleKeyUp(KeyCode key)
        {

        }
    }
}
