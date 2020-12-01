using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotKeyHUD
{
    public class HotKeySetupWindow : DaggerfallPopupWindow
    {
        ItemListScroller itemListScroller;
        Rect itemListScrollerRect = new Rect(253, 49, 60, 148);

        public HotKeySetupWindow(IUserInterfaceManager uiManager) : base(uiManager)
        {
        }

        protected override void Setup()
        {
            if (IsSetup)
                return;

            itemListScroller = new ItemListScroller(defaultToolTip)
            {
                Position = new Vector2(itemListScrollerRect.x, itemListScrollerRect.y),
                Size = new Vector2(itemListScrollerRect.width, itemListScrollerRect.height)
            };

            IsSetup = true;
        }
    }
}
