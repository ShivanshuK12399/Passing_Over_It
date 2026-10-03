using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FishNet;
using FishNet.Managing.Timing;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// In-Game HUD overlay displaying Dedicated Server IP:Port connection details, ping, and server status.
    /// </summary>
    [DisallowMultipleComponent]
    public class InGameNetworkHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text serverInfoText;
        [SerializeField] private TMP_Text pingText;
        [SerializeField] private Button copyInfoButton;

        private TimeManager timeManager;

        private void Start()
        {

            if (copyInfoButton != null)
            {
                copyInfoButton.onClick.AddListener(OnCopyInfoClicked);
            }

            if (InstanceFinder.NetworkManager != null)
            {
                timeManager = InstanceFinder.TimeManager;
            }

            UpdateServerInfoDisplay();
        }

        private void Update()
        {
            if (pingText != null && timeManager != null)
            {
                long roundTripTimeMs = timeManager.RoundTripTime;
                pingText.text = $"Ping: {roundTripTimeMs} ms";
            }
        }

        private void UpdateServerInfoDisplay()
        {
            if (serverInfoText != null)
            {
                if (DedicatedServerManager.Instance != null)
                {
                    if (DedicatedServerManager.Instance.IsDedicatedServer)
                    {
                        serverInfoText.text = $"[SERVER MODE] Port: {DedicatedServerManager.Instance.ActivePort}";
                    }
                    else
                    {
                        serverInfoText.text = $"Server: {DedicatedServerManager.Instance.ActiveServerIP}:{DedicatedServerManager.Instance.ActivePort}";
                    }
                }
                else
                {
                    serverInfoText.text = "Server: Connected";
                }
            }
        }

        private void OnCopyInfoClicked()
        {
            string infoToCopy = serverInfoText != null ? serverInfoText.text : "127.0.0.1:7777";
            GUIUtility.systemCopyBuffer = infoToCopy;

            if (serverInfoText != null)
            {
                serverInfoText.text = $"COPIED: {infoToCopy}";
            }
            Invoke(nameof(UpdateServerInfoDisplay), 2.0f);
        }
    }
}
