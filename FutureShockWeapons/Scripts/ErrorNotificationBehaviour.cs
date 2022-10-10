using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace FutureShock
{
    public sealed class ErrorNotificationBehaviour : MonoBehaviour
    {
        static private bool showedMessage = false;
        private void Update()
        {
            const string errorMessage = "Future Shock Weapons could not load. See log for details.";
            if (!showedMessage && GameManager.Instance.StateManager.CurrentState == StateManager.StateTypes.Game)
            {
                var messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallMessageBox.CommonMessageBoxButtons.Nothing, errorMessage);
                messageBox.ClickAnywhereToClose = true;
                messageBox.OnClose += MessageBox_OnClose;
                messageBox.Show();
                showedMessage = true;
            }
        }

        private void MessageBox_OnClose()
        {
            Destroy(this);
        }
    }
}
