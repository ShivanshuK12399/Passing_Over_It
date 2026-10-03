using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FishNet;
using FishNet.Managing.Scened;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Menu UI controller for Dedicated Server connections.
    /// Allows inputting Server IP & Port, quick one-click Localhost (127.0.0.1:7777) testing,
    /// and initiating local server processes.
    /// </summary>
    [DisallowMultipleComponent]
    public class MultiplayerMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button connectButton;
        [SerializeField] private Button localhostConnectButton;
        [SerializeField] private Button startLocalServerButton;

        [Header("Inputs & Displays")]
        [SerializeField] private TMP_InputField serverIpInputField;
        [SerializeField] private TMP_InputField serverPortInputField;
        [SerializeField] private TMP_Text statusMessageText;

        [Header("Preset Options")]
        [SerializeField] private string defaultLocalhostIP = "127.0.0.1";
        [SerializeField] private string defaultPort = "7777";
        [SerializeField] private int targetSceneIndex = 1;

        private void Start()
        {
            SetupInputDefaults();
            SetupButtonListeners();
            UpdateStatus("Ready to connect to Dedicated Server.");
        }

        private void OnEnable()
        {
            if (DedicatedServerManager.Instance != null)
            {
                DedicatedServerManager.Instance.OnClientConnectedEvent += HandleClientConnected;
                DedicatedServerManager.Instance.OnClientDisconnectedEvent += HandleClientDisconnected;
                DedicatedServerManager.Instance.OnConnectionErrorEvent += HandleConnectionError;
                DedicatedServerManager.Instance.OnServerStartedEvent += HandleServerStarted;
            }
        }

        private void OnDisable()
        {
            if (DedicatedServerManager.Instance != null)
            {
                DedicatedServerManager.Instance.OnClientConnectedEvent -= HandleClientConnected;
                DedicatedServerManager.Instance.OnClientDisconnectedEvent -= HandleClientDisconnected;
                DedicatedServerManager.Instance.OnConnectionErrorEvent -= HandleConnectionError;
                DedicatedServerManager.Instance.OnServerStartedEvent -= HandleServerStarted;
            }
        }

        private void SetupInputDefaults()
        {
            if (serverIpInputField != null && string.IsNullOrWhiteSpace(serverIpInputField.text))
            {
                serverIpInputField.text = defaultLocalhostIP;
            }
            if (serverPortInputField != null && string.IsNullOrWhiteSpace(serverPortInputField.text))
            {
                serverPortInputField.text = defaultPort;
            }
        }

        private void SetupButtonListeners()
        {
            if (connectButton != null) connectButton.onClick.AddListener(OnConnectClicked);
            if (localhostConnectButton != null) localhostConnectButton.onClick.AddListener(OnLocalhostConnectClicked);
            if (startLocalServerButton != null) startLocalServerButton.onClick.AddListener(OnStartLocalServerClicked);
        }

        public void OnConnectClicked()
        {
            string ip = serverIpInputField != null ? serverIpInputField.text.Trim() : defaultLocalhostIP;
            string portStr = serverPortInputField != null ? serverPortInputField.text.Trim() : defaultPort;

            if (!ushort.TryParse(portStr, out ushort port))
            {
                port = ServerCommandLineArgs.DefaultPort;
            }

            SetButtonsInteractable(false);
            UpdateStatus($"Connecting to Dedicated Server at {ip}:{port}...");

            if (DedicatedServerManager.Instance != null)
            {
                DedicatedServerManager.Instance.StartClient(ip, port);
            }
            else
            {
                UpdateStatus("Error: DedicatedServerManager not found in scene!");
                SetButtonsInteractable(true);
            }
        }

        public void OnLocalhostConnectClicked()
        {
            if (serverIpInputField != null) serverIpInputField.text = defaultLocalhostIP;
            if (serverPortInputField != null) serverPortInputField.text = defaultPort;

            OnConnectClicked();
        }

        public void OnStartLocalServerClicked()
        {
            string portStr = serverPortInputField != null ? serverPortInputField.text.Trim() : defaultPort;
            if (!ushort.TryParse(portStr, out ushort port))
            {
                port = ServerCommandLineArgs.DefaultPort;
            }

            SetButtonsInteractable(false);
            UpdateStatus($"Starting local server on port {port}...");

            if (DedicatedServerManager.Instance != null)
            {
                DedicatedServerManager.Instance.StartDedicatedServer(port);
            }
            else
            {
                UpdateStatus("Error: DedicatedServerManager not found in scene!");
                SetButtonsInteractable(true);
            }
        }

        private void HandleClientConnected()
        {
            UpdateStatus("Successfully connected to Dedicated Server! Entering Arena...");
            SetButtonsInteractable(true);
        }

        private void HandleClientDisconnected()
        {
            UpdateStatus("Disconnected from Dedicated Server.");
            SetButtonsInteractable(true);
        }

        private void HandleServerStarted()
        {
            UpdateStatus($"Local Dedicated Server started successfully on port {DedicatedServerManager.Instance?.ActivePort ?? 7777}.");
            SetButtonsInteractable(true);
        }

        private void HandleConnectionError(string message)
        {
            UpdateStatus($"Connection Failed: {message}");
            SetButtonsInteractable(true);
        }

        private void UpdateStatus(string text)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = text;
            }
            Debug.Log($"[MultiplayerMenuUI] {text}");
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (connectButton != null) connectButton.interactable = interactable;
            if (localhostConnectButton != null) localhostConnectButton.interactable = interactable;
            if (startLocalServerButton != null) startLocalServerButton.interactable = interactable;
        }
    }
}
