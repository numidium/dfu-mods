using DaggerfallWorkshop.Game;
using System;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HotKeyInput : MonoBehaviour
    {
        public HotKeyUtil.KeyCodeHandler KeyDownHandler;
        public HotKeyUtil.BlankHandler SpellAbortHandler;
        private InputManager inputManager;

        private void Start()
        {
            inputManager = InputManager.Instance;
        }

        private void Update()
        {
            var keyDown = inputManager.GetAnyKeyDown();
            if (keyDown != KeyCode.None)
                RaiseKeyDownHandler(keyDown);
            if (inputManager.ActionStarted(InputManager.Actions.AbortSpell))
                RaiseSpellAbortHandler();
        }

        private void RaiseKeyDownHandler(KeyCode keyCode)
        {
            KeyDownHandler?.Invoke(keyCode);
        }

        private void RaiseSpellAbortHandler()
        {
            SpellAbortHandler?.Invoke();
        }
    }
}
