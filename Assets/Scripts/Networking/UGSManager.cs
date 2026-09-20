using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using FishNet;
using FishNet.Managing;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Singleton manager handling Unity Gaming Services (UGS) Core, Authentication,
    /// and Relay Allocation setup for Fish-Net networking.
    /// </summary>
    [DisallowMultipleComponent]
    public class UGSManager : MonoBehaviour
    {
        public static UGSManager Instance { get; private set; }

        public event Action OnAuthenticated;
        public event Action<string> OnAuthFailed;
        public event Action<string> OnRelayHostCreated;
        public event Action OnRelayJoined;
        public event Action<string> OnRelayFailed;

        public bool IsAuthenticated { get; private set; }
        public string HostJoinCode { get; private set; }

        [Header("Fish-Net Reference")]
        [SerializeField] private NetworkManager networkManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await InitializeAndAuthenticateAsync();
        }

        /// <summary>
        /// Initializes UGS Core & performs anonymous authentication.
        /// </summary>
        public async Task InitializeAndAuthenticateAsync()
        {
            if (IsAuthenticated) return;

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                IsAuthenticated = true;
                Debug.Log($"[UGSManager] Successfully authenticated anonymously. PlayerID: {AuthenticationService.Instance.PlayerId}");
                OnAuthenticated?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UGSManager] UGS Authentication failed: {ex.Message}");
                OnAuthFailed?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Creates a UGS Relay allocation for Host, sets up Fish-Net transport, and starts Host.
        /// </summary>
        public async Task<string> StartRelayHostAsync(int maxPlayers = 10)
        {
            if (!IsAuthenticated)
            {
                await InitializeAndAuthenticateAsync();
                if (!IsAuthenticated)
                {
                    OnRelayFailed?.Invoke("Authentication required before hosting.");
                    return null;
                }
            }

            try
            {
                // Allocation count excludes host, so maxPlayers - 1
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                HostJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                Debug.Log($"[UGSManager] Relay Allocation created successfully! Join Code: {HostJoinCode}");

                EnsureNetworkManagerReference();

                if (networkManager != null)
                {
                    // Start Host on Fish-Net
                    networkManager.ServerManager.StartConnection();
                    networkManager.ClientManager.StartConnection();
                }

                OnRelayHostCreated?.Invoke(HostJoinCode);
                return HostJoinCode;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UGSManager] Failed to create Relay Host: {ex.Message}");
                OnRelayFailed?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Joins an existing UGS Relay allocation via Join Code and connects Fish-Net Client.
        /// </summary>
        public async Task<bool> StartRelayClientAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                OnRelayFailed?.Invoke("Join Code cannot be empty.");
                return false;
            }

            if (!IsAuthenticated)
            {
                await InitializeAndAuthenticateAsync();
                if (!IsAuthenticated)
                {
                    OnRelayFailed?.Invoke("Authentication required before joining.");
                    return false;
                }
            }

            try
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());
                Debug.Log($"[UGSManager] Joined Relay Allocation successfully for code: {joinCode}");

                EnsureNetworkManagerReference();

                if (networkManager != null)
                {
                    // Start Client connection on Fish-Net
                    networkManager.ClientManager.StartConnection();
                }

                OnRelayJoined?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UGSManager] Failed to join Relay allocation: {ex.Message}");
                OnRelayFailed?.Invoke(ex.Message);
                return false;
            }
        }

        private void EnsureNetworkManagerReference()
        {
            if (networkManager == null)
            {
                networkManager = InstanceFinder.NetworkManager;
            }
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
            }
        }
    }
}
