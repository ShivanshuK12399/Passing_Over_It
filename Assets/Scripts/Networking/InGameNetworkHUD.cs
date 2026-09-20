using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// In-Game HUD overlay for displaying the Room Code, auto-copy button,
    /// and connection status while playing in the Arena.
    /// </summary>
    [DisallowMultipleComponent]
    public class InGameNetworkHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private Button copyCodeButton;

        private void Start()
        {
            FindReferencesIfUnassigned();

            if (copyCodeButton != null)
            {
                copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
            }

            UpdateRoomCodeDisplay();
        }

        private void FindReferencesIfUnassigned()
        {
            if (roomCodeText == null)
            {
                TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
                foreach (var txt in texts)
                {
                    if (txt.name.ToLower().Contains("code") || txt.name.ToLower().Contains("room") || txt.name.ToLower().Contains("hud"))
                    {
                        roomCodeText = txt;
                        break;
                    }
                }
            }

            if (copyCodeButton == null)
            {
                Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
                foreach (var btn in buttons)
                {
                    if (btn.name.ToLower().Contains("copy") || btn.name.ToLower().Contains("code"))
                    {
                        copyCodeButton = btn;
                        break;
                    }
                }
            }
        }

        private void UpdateRoomCodeDisplay()
        {
            if (LobbyManager.Instance != null && LobbyManager.Instance.JoinedLobby != null)
            {
                string code = LobbyManager.Instance.JoinedLobby.LobbyCode;
                if (roomCodeText != null)
                {
                    roomCodeText.text = $"{code}";
                }
            }
            else if (UGSManager.Instance != null && !string.IsNullOrEmpty(UGSManager.Instance.HostJoinCode))
            {
                if (roomCodeText != null)
                {
                    roomCodeText.text = $"{UGSManager.Instance.HostJoinCode}";
                }
            }
        }

        private void OnCopyCodeClicked()
        {
            string codeToCopy = null;
            if (LobbyManager.Instance != null && LobbyManager.Instance.JoinedLobby != null)
            {
                codeToCopy = LobbyManager.Instance.JoinedLobby.LobbyCode;
            }
            else if (UGSManager.Instance != null)
            {
                codeToCopy = UGSManager.Instance.HostJoinCode;
            }

            if (!string.IsNullOrEmpty(codeToCopy))
            {
                GUIUtility.systemCopyBuffer = codeToCopy;
                if (roomCodeText != null)
                {
                    roomCodeText.text = $"COPIED: {codeToCopy}";
                }
                Invoke(nameof(UpdateRoomCodeDisplay), 2.0f);
            }
        }
    }
}
