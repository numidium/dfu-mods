using DaggerfallWorkshop.Game;
using UnityEngine;

namespace HotKeyHUD
{
    public sealed class HKHInput : MonoBehaviour
    {
        public HKHUtil.KeyCodeHandler KeyDownHandler;
        public HKHUtil.KeyCodeHandler KeyUpHandler;
        public HKHUtil.BlankHandler SpellAbortHandler;
        public HKHUtil.BlankHandler ReadyWeaponHandler;
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
            var keyUp = inputManager.GetAnyKeyUp();
            if (keyUp != KeyCode.None)
                RaiseKeyUpHandler(keyUp);
            if (inputManager.ActionStarted(InputManager.Actions.AbortSpell))
                RaiseSpellAbortHandler();
            if (inputManager.ActionStarted(InputManager.Actions.ReadyWeapon))
                RaiseReadyWeaponHandler();
        }

        private void RaiseKeyDownHandler(KeyCode keyCode)
        {
            KeyDownHandler?.Invoke(keyCode);
        }

        private void RaiseKeyUpHandler(KeyCode keyCode)
        {
            KeyUpHandler?.Invoke(keyCode);
        }

        private void RaiseSpellAbortHandler()
        {
            SpellAbortHandler?.Invoke();
        }

        private void RaiseReadyWeaponHandler()
        {
            ReadyWeaponHandler?.Invoke();
        }
    }
}
