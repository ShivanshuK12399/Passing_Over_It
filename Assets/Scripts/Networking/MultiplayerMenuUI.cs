using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;
using FishNet;
using FishNet.Managing.Scened;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Streamlined Canvas UI controller for Direct-to-Arena Multiplayer Host & Join flow.
    /// Immediately loads Scene 1 (Build Index 1) upon hosting or joining, copying the room code to clipboard.
    /// </summary>
    [DisallowMultipleComponent]
    public class MultiplayerMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button quickJoinButton;

        [Header("Inputs & Displays")]
        [SerializeField] private TMP_InputField roomCodeInputField;
        [SerializeField] private TMP_Text statusMessageText;
        [SerializeField] private TMP_InputField lobbyNameInputField;

        [Header("Settings & Scene Flow")]
        [SerializeField] private int maxPlayersPerLobby = 20;
        [SerializeField] private bool loadSceneOnConnect = true;
        [SerializeField] private int targetSceneIndex = 1;

        private void Start()
        {
            SetupButtonListeners();
            UpdateStatus("Ready to connect.");
        }

        private void OnEnable()
        {
            if (UGSManager.Instance != null)
            {
                UGSManager.Instance.OnAuthFailed += HandleAuthFailed;
                UGSManager.Instance.OnRelayFailed += HandleRelayFailed;
            }

            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLobbyError += HandleLobbyError;
            }
        }

        private void OnDisable()
        {
            if (UGSManager.Instance != null)
            {
                UGSManager.Instance.OnAuthFailed -= HandleAuthFailed;
                UGSManager.Instance.OnRelayFailed -= HandleRelayFailed;
            }

            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLobbyError -= HandleLobbyError;
            }
        }

        private void SetupButtonListeners()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(OnQuickJoinClicked);
        }

        public async void OnHostClicked()
        {
            SetButtonsInteractable(false);
            UpdateStatus("Initializing Host & Relay...");

            string relayCode = await UGSManager.Instance.StartRelayHostAsync(maxPlayersPerLobby);
            if (string.IsNullOrEmpty(relayCode))
            {
                SetButtonsInteractable(true);
                return;
            }

            UpdateStatus("Creating UGS Lobby...");
            string lobbyName = lobbyNameInputField != null && !string.IsNullOrWhiteSpace(lobbyNameInputField.text)
                ? lobbyNameInputField.text
                : "Passing Over It Lobby";

            Lobby lobby = await LobbyManager.Instance.CreateLobbyAsync(lobbyName, maxPlayersPerLobby, relayCode);
            if (lobby != null)
            {
                // Auto-copy Room Code to clipboard for easy sharing
                GUIUtility.systemCopyBuffer = lobby.LobbyCode;
                UpdateStatus($"Lobby Created! Code '{lobby.LobbyCode}' copied to clipboard.");

                if (loadSceneOnConnect)
                {
                    UpdateStatus($"Entering Arena (Build Index {targetSceneIndex})...");
                    LoadGameSceneServer();
                }
            }

            SetButtonsInteractable(true);
        }

        public async void OnJoinClicked()
        {
            if (roomCodeInputField == null || string.IsNullOrWhiteSpace(roomCodeInputField.text))
            {
                UpdateStatus("Please enter a valid Room Code.");
                return;
            }

            string inputCode = roomCodeInputField.text.Trim();
            SetButtonsInteractable(false);
            UpdateStatus($"Joining Lobby '{inputCode}'...");

            Lobby lobby = await LobbyManager.Instance.JoinLobbyByCodeAsync(inputCode);
            if (lobby == null)
            {
                SetButtonsInteractable(true);
                return;
            }

            UpdateStatus("Connecting to Relay Server...");
            string relayCode = LobbyManager.Instance.GetRelayJoinCode(lobby);
            bool success = await UGSManager.Instance.StartRelayClientAsync(relayCode);

            if (success)
            {
                UpdateStatus($"Connected! Entering Arena...");
            }

            SetButtonsInteractable(true);
        }

        public async void OnQuickJoinClicked()
        {
            SetButtonsInteractable(false);
            UpdateStatus("Searching for open public lobby...");

            Lobby lobby = await LobbyManager.Instance.QuickJoinLobbyAsync();
            if (lobby == null)
            {
                SetButtonsInteractable(true);
                return;
            }

            UpdateStatus("Connecting to Relay Server...");
            string relayCode = LobbyManager.Instance.GetRelayJoinCode(lobby);
            bool success = await UGSManager.Instance.StartRelayClientAsync(relayCode);

            if (success)
            {
                UpdateStatus($"Quick Joined! Entering Arena...");
            }

            SetButtonsInteractable(true);
        }

        private void LoadGameSceneServer()
        {
            string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(targetSceneIndex);
            string sceneName = !string.IsNullOrEmpty(scenePath)
                ? System.IO.Path.GetFileNameWithoutExtension(scenePath)
                : "GameScene";

            if (InstanceFinder.SceneManager != null)
            {
                SceneLoadData sld = new SceneLoadData(sceneName)
                {
                    ReplaceScenes = ReplaceOption.All
                };
                InstanceFinder.SceneManager.LoadGlobalScenes(sld);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneIndex);
            }
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
            if (hostButton != null) hostButton.interactable = interactable;
            if (joinButton != null) joinButton.interactable = interactable;
            if (quickJoinButton != null) quickJoinButton.interactable = interactable;
        }

        private void HandleAuthFailed(string msg) => UpdateStatus($"Auth Error: {msg}");
        private void HandleRelayFailed(string msg) => UpdateStatus($"Relay Error: {msg}");
        private void HandleLobbyError(string msg) => UpdateStatus($"Lobby Error: {msg}");
    }
}
