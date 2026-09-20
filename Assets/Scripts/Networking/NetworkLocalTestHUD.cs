using UnityEngine;
using FishNet;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Local Offline / LAN Dev Testing HUD attached in GameScene.
    /// Allows starting Play Mode directly inside GameScene with 0ms local Tugboat connection (127.0.0.1),
    /// completely bypassing UGS Relay / Internet authentication delays.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkLocalTestHUD : MonoBehaviour
    {
        [Header("Dev Auto-Start Settings")]
        [Tooltip("Auto-start Host immediately when entering Play Mode in GameScene.")]
        [SerializeField] private bool autoHostOnPlay = false;

        [Tooltip("Local IP for client connection.")]
        [SerializeField] private string localIp = "127.0.0.1";

        [Header("UI Toggle")]
        [SerializeField] private bool showDevGUI = true;

        private void Awake()
        {
            // Disable Local Dev HUD if connection was initiated from MainMenu
            if ((InstanceFinder.NetworkManager != null && (InstanceFinder.IsServerStarted || InstanceFinder.IsClientStarted)) ||
                (LobbyManager.Instance != null && LobbyManager.Instance.JoinedLobby != null))
            {
                Debug.Log("[NetworkLocalTestHUD] Transitioned from MainMenu -> Disabling Local Dev Testing HUD.");
                showDevGUI = false;
                enabled = false;
            }
        }

        private void Start()
        {
            EnsureNetworkManagerPresent();

            if (autoHostOnPlay && InstanceFinder.NetworkManager != null && !InstanceFinder.IsServerStarted)
            {
                StartLocalHost();
            }
        }

        private void EnsureNetworkManagerPresent()
        {
            if (InstanceFinder.NetworkManager == null)
            {
                Debug.LogWarning("[NetworkLocalTestHUD] NetworkManager is not present in GameScene. Please add [NetworkManager] to GameScene.");
            }
        }

        public void StartLocalHost()
        {
            EnsureNetworkManagerPresent();
            if (InstanceFinder.NetworkManager != null)
            {
                InstanceFinder.ServerManager.StartConnection();
                InstanceFinder.ClientManager.StartConnection();
                Debug.Log("[NetworkLocalTestHUD] Started Local Tugboat Host (127.0.0.1)!");
            }
        }

        public void StartLocalClient()
        {
            EnsureNetworkManagerPresent();
            if (InstanceFinder.NetworkManager != null)
            {
                InstanceFinder.ClientManager.StartConnection(localIp);
                Debug.Log($"[NetworkLocalTestHUD] Connecting to Local Tugboat Server ({localIp})...");
            }
        }

        public void StopConnection()
        {
            if (InstanceFinder.NetworkManager != null)
            {
                if (InstanceFinder.IsServerStarted) InstanceFinder.ServerManager.StopConnection(true);
                if (InstanceFinder.IsClientStarted) InstanceFinder.ClientManager.StopConnection();
            }
        }

        private void OnGUI()
        {
            if (!showDevGUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 220, 140), "Local Dev HUD (Tugboat)", GUI.skin.window);

            if (InstanceFinder.NetworkManager == null)
            {
                GUILayout.Label("Loading NetworkManager...");
            }
            else if (!InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted)
            {
                if (GUILayout.Button("Host Local (127.0.0.1)", GUILayout.Height(30)))
                {
                    StartLocalHost();
                }

                if (GUILayout.Button("Join Local (127.0.0.1)", GUILayout.Height(30)))
                {
                    StartLocalClient();
                }
            }
            else
            {
                string status = InstanceFinder.IsServerStarted ? "Host/Server Active" : "Client Connected";
                GUILayout.Label($"Status: {status}");

                if (GUILayout.Button("Disconnect", GUILayout.Height(25)))
                {
                    StopConnection();
                }
            }

            GUILayout.EndArea();
        }
    }
}
