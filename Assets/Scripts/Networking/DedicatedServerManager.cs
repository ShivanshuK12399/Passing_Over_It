using System;
using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Singleton manager controlling Dedicated Cloud Server and Direct IP Client connections for Fish-Net.
    /// Handles CLI flags auto-bootstrap, Tugboat transport configuration, local PC testing, and VPS deployment.
    /// </summary>
    [DisallowMultipleComponent]
    public class DedicatedServerManager : MonoBehaviour
    {
        public static DedicatedServerManager Instance { get; private set; }

        public event Action OnServerStartedEvent;
        public event Action OnServerStoppedEvent;
        public event Action OnClientConnectedEvent;
        public event Action OnClientDisconnectedEvent;
        public event Action<string> OnConnectionErrorEvent;

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Default Configs")]
        [SerializeField] private ushort defaultPort = ServerCommandLineArgs.DefaultPort;
        [SerializeField] private string defaultConnectIP = ServerCommandLineArgs.DefaultConnectIP;
        [SerializeField] private int targetGameSceneIndex = 1;
        [SerializeField] private bool autoLoadSceneOnServerStart = true;

        public ushort ActivePort { get; private set; } = ServerCommandLineArgs.DefaultPort;
        public string ActiveServerIP { get; private set; } = ServerCommandLineArgs.DefaultConnectIP;
        public bool IsDedicatedServer { get; private set; }

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

        private void Start()
        {
            EnsureNetworkManagerReference();
            RegisterNetworkEvents();

            // Auto-bootstrap Dedicated Server if launched with CLI flags (-batchmode / -dedicated / -port)
            if (ServerCommandLineArgs.IsDedicatedServerRequested())
            {
                IsDedicatedServer = true;
                ushort cliPort = ServerCommandLineArgs.GetPort();
                Debug.Log($"[DedicatedServerManager] CLI dedicated server detected! Bootstrapping server on port {cliPort}...");
                StartDedicatedServer(cliPort);
            }
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
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

        private void RegisterNetworkEvents()
        {
            if (networkManager == null) return;

            networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;
            networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
        }

        private void UnregisterNetworkEvents()
        {
            if (networkManager == null) return;

            networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
            networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
        }

        /// <summary>
        /// Configures Fish-Net Tugboat transport and starts Dedicated Server.
        /// </summary>
        public bool StartDedicatedServer(ushort port = 0)
        {
            EnsureNetworkManagerReference();
            if (networkManager == null)
            {
                Debug.LogError("[DedicatedServerManager] Cannot start server: NetworkManager is missing!");
                OnConnectionErrorEvent?.Invoke("NetworkManager reference missing.");
                return false;
            }

            ActivePort = port > 0 ? port : defaultPort;

            ConfigureTugboatServer(ActivePort);

            Debug.Log($"[DedicatedServerManager] Starting Dedicated Server on Port {ActivePort}...");
            bool success = networkManager.ServerManager.StartConnection();
            if (!success)
            {
                Debug.LogError("[DedicatedServerManager] Failed to start Fish-Net Server Connection.");
                OnConnectionErrorEvent?.Invoke("Failed to start server connection.");
            }
            return success;
        }

        /// <summary>
        /// Configures Fish-Net Tugboat transport and connects Client to Dedicated Server IP:Port.
        /// </summary>
        public bool StartClient(string serverIp, ushort port = 0)
        {
            EnsureNetworkManagerReference();
            if (networkManager == null)
            {
                Debug.LogError("[DedicatedServerManager] Cannot start client: NetworkManager is missing!");
                OnConnectionErrorEvent?.Invoke("NetworkManager reference missing.");
                return false;
            }

            ActiveServerIP = string.IsNullOrWhiteSpace(serverIp) ? defaultConnectIP : serverIp.Trim();
            ActivePort = port > 0 ? port : defaultPort;

            ConfigureTugboatClient(ActiveServerIP, ActivePort);

            Debug.Log($"[DedicatedServerManager] Connecting Client to {ActiveServerIP}:{ActivePort}...");
            bool success = networkManager.ClientManager.StartConnection();
            if (!success)
            {
                Debug.LogError("[DedicatedServerManager] Failed to start Fish-Net Client Connection.");
                OnConnectionErrorEvent?.Invoke("Failed to start client connection.");
            }
            return success;
        }

        /// <summary>
        /// Stops active Client and/or Server connections.
        /// </summary>
        public void StopConnection()
        {
            if (networkManager == null) return;

            if (networkManager.ServerManager.Started)
            {
                networkManager.ServerManager.StopConnection(true);
            }
            if (networkManager.ClientManager.Started)
            {
                networkManager.ClientManager.StopConnection();
            }
        }

        private void ConfigureTugboatServer(ushort port)
        {
            if (networkManager == null) return;

            Transport transport = networkManager.TransportManager.Transport;
            if (transport is Tugboat tugboat)
            {
                tugboat.SetPort(port);
                Debug.Log($"[DedicatedServerManager] Tugboat transport configured for Server on port {port}.");
            }
            else
            {
                Debug.LogWarning($"[DedicatedServerManager] Active transport is not Tugboat ({transport?.GetType().Name}). Ensure port settings are applied.");
            }
        }

        private void ConfigureTugboatClient(string ip, ushort port)
        {
            if (networkManager == null) return;

            Transport transport = networkManager.TransportManager.Transport;
            if (transport is Tugboat tugboat)
            {
                tugboat.SetPort(port);
                tugboat.SetClientAddress(ip);
                Debug.Log($"[DedicatedServerManager] Tugboat transport configured for Client: {ip}:{port}.");
            }
            else
            {
                Debug.LogWarning($"[DedicatedServerManager] Active transport is not Tugboat ({transport?.GetType().Name}). Ensure address settings are applied.");
            }
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            Debug.Log($"[DedicatedServerManager] Server Connection State: {args.ConnectionState}");
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                OnServerStartedEvent?.Invoke();

                if (autoLoadSceneOnServerStart && networkManager.ServerManager.Started)
                {
                    LoadGameSceneForServer();
                }
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                OnServerStoppedEvent?.Invoke();
            }
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            Debug.Log($"[DedicatedServerManager] Client Connection State: {args.ConnectionState}");
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                OnClientConnectedEvent?.Invoke();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                OnClientDisconnectedEvent?.Invoke();
            }
        }

        private void LoadGameSceneForServer()
        {
            string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(targetGameSceneIndex);
            string sceneName = !string.IsNullOrEmpty(scenePath)
                ? System.IO.Path.GetFileNameWithoutExtension(scenePath)
                : "GameScene";

            Debug.Log($"[DedicatedServerManager] Dedicated Server auto-loading Arena scene '{sceneName}' (Build Index {targetGameSceneIndex})...");

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
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetGameSceneIndex);
            }
        }
    }
}
