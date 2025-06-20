using DaggerfallWorkshop.Game;
using System;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HotKeyInput : MonoBehaviour
    {
        public EventHandler<KeyCode> KeyDownHandler;

        private void Update()
        {
            var keyDown = InputManager.Instance.GetAnyKeyDown();
            if (keyDown != KeyCode.None)
                RaiseKeyDownHandler(keyDown);
        }

        private void RaiseKeyDownHandler(KeyCode key)
        {
            KeyDownHandler?.Invoke(this, key);
        }
    }
}
