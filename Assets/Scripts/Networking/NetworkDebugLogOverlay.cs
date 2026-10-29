using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Connection;
using PassingOverIt.Player;

namespace PassingOverIt.Networking
{
    /// <summary>
    /// Self-contained in-game debug log overlay for both PC Standalone Server and Android Client.
    /// Captures all Unity logs/exceptions, FishNet connection state events, and player telemetry.
    /// Defaults to collapsed with a red unread error badge so it never obstructs gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkDebugLogOverlay : MonoBehaviour
    {
        public static NetworkDebugLogOverlay Instance { get; private set; }

        private struct LogEntry
        {
            public string Timestamp;
            public LogType Type;
            public string Message;
            public string StackTrace;
        }

        private const int MaxLogCount = 150;
        private readonly List<LogEntry> _logs = new List<LogEntry>(MaxLogCount);
        private readonly object _lock = new object();

        private bool _isExpanded = false;
        private int _unreadErrorCount = 0;
        private Vector2 _scrollPos = Vector2.zero;
        private LogFilter _currentFilter = LogFilter.All;

        private enum LogFilter
        {
            All,
            Errors,
            Network,
            Telemetry
        }

        // GUI Styles
        private GUIStyle _windowStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _badgeButtonStyle;
        private GUIStyle _logStyle;
        private GUIStyle _telemetryStyle;
        private bool _stylesInitialized = false;

        // Telemetry Cache
        private float _telemetryTimer = 0f;
        private string _cachedTelemetry = "Searching for Player...";

        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            GameObject go = new GameObject("[NetworkDebugLogOverlay]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NetworkDebugLogOverlay>();
        }

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

        private void OnEnable()
        {
            Application.logMessageReceivedThreaded += HandleLogMessage;

            RegisterFishNetEvents();
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= HandleLogMessage;

            UnregisterFishNetEvents();
        }

        private void Update()
        {
            // Continuously try to register FishNet events if NetworkManager starts late
            EnsureFishNetRegistered();

            // Update telemetry every 0.2s
            _telemetryTimer -= Time.unscaledDeltaTime;
            if (_telemetryTimer <= 0f)
            {
                _telemetryTimer = 0.2f;
                UpdateTelemetry();
            }
        }

        #region FishNet Event Handling

        private bool _isFishNetHooked = false;

        private void EnsureFishNetRegistered()
        {
            if (!_isFishNetHooked && InstanceFinder.NetworkManager != null)
            {
                RegisterFishNetEvents();
            }
        }

        private void RegisterFishNetEvents()
        {
            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null || _isFishNetHooked) return;

            nm.ServerManager.OnServerConnectionState += HandleServerState;
            nm.ServerManager.OnRemoteConnectionState += HandleRemoteClientState;
            nm.ClientManager.OnClientConnectionState += HandleClientState;

            _isFishNetHooked = true;
            AddManualLog(LogType.Log, "[NET] Hooked into FishNet NetworkManager events.");
        }

        private void UnregisterFishNetEvents()
        {
            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null || !_isFishNetHooked) return;

            nm.ServerManager.OnServerConnectionState -= HandleServerState;
            nm.ServerManager.OnRemoteConnectionState -= HandleRemoteClientState;
            nm.ClientManager.OnClientConnectionState -= HandleClientState;

            _isFishNetHooked = false;
        }

        private void HandleServerState(ServerConnectionStateArgs args)
        {
            AddManualLog(LogType.Log, $"[SERVER EVENT] Server Connection State: {args.ConnectionState}");
        }

        private void HandleRemoteClientState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            int clientId = conn != null ? conn.ClientId : -1;
            LogType type = args.ConnectionState == RemoteConnectionState.Stopped ? LogType.Warning : LogType.Log;
            AddManualLog(type, $"[SERVER EVENT] Remote Client ID {clientId} -> {args.ConnectionState}");
        }

        private void HandleClientState(ClientConnectionStateArgs args)
        {
            LogType type = args.ConnectionState == LocalConnectionState.Stopped ? LogType.Error : LogType.Log;
            AddManualLog(type, $"[CLIENT EVENT] Local Client State: {args.ConnectionState}");
        }

        #endregion

        #region Logging

        private void HandleLogMessage(string logString, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                if (_logs.Count >= MaxLogCount)
                {
                    _logs.RemoveAt(0);
                }

                _logs.Add(new LogEntry
                {
                    Timestamp = DateTime.Now.ToString("HH:mm:ss.ff"),
                    Type = type,
                    Message = logString,
                    StackTrace = stackTrace
                });

                if (type == LogType.Error || type == LogType.Exception)
                {
                    _unreadErrorCount++;
                }
            }
        }

        public void AddManualLog(LogType type, string message)
        {
            HandleLogMessage(message, string.Empty, type);
        }

        private void UpdateTelemetry()
        {
            StringBuilder sb = new StringBuilder();

            bool isServer = InstanceFinder.IsServerStarted;
            bool isClient = InstanceFinder.IsClientStarted;

            sb.Append($"Role: {(isServer ? "SERVER" : "")}{(isServer && isClient ? " + " : "")}{(isClient ? "CLIENT" : (!isServer ? "OFFLINE" : ""))}");

            if (InstanceFinder.TimeManager != null)
            {
                sb.Append($" | Ping: {InstanceFinder.TimeManager.RoundTripTime}ms | Tick: {InstanceFinder.TimeManager.LocalTick}");
            }

            // Find local player or any spawned player
            PlayerController localPlayer = null;
            for (int i = 0; i < PlayerController.AllPlayers.Count; i++)
            {
                var p = PlayerController.AllPlayers[i];
                if (p != null && (p.IsLocalDriver || localPlayer == null))
                {
                    localPlayer = p;
                    if (p.IsLocalDriver) break;
                }
            }

            if (localPlayer != null)
            {
                Vector3 pos = localPlayer.transform.position;
                sb.AppendLine();
                sb.Append($"Player [{(localPlayer.IsLocalDriver ? "LocalDriver" : $"ID:{localPlayer.ObjectId}")}]: Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) | Grounded: {localPlayer.IsGrounded} | Eliminated: {localPlayer.IsEliminated.Value}");
            }
            else
            {
                sb.AppendLine();
                sb.Append("No Player Object found in scene.");
            }

            _cachedTelemetry = sb.ToString();
        }

        #endregion

        #region OnGUI Interface

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = 12
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            _badgeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _badgeButtonStyle.normal.textColor = Color.red;

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };

            _telemetryStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // Screen scale adaptation for mobile high-DPI displays
            float scale = Mathf.Max(1.0f, Screen.dpi > 0 ? Screen.dpi / 160f : Screen.height / 720f);
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

            float virtualWidth = Screen.width / scale;
            float virtualHeight = Screen.height / scale;

            if (!_isExpanded)
            {
                DrawCollapsedBadge(virtualWidth, virtualHeight);
            }
            else
            {
                DrawExpandedWindow(virtualWidth, virtualHeight);
            }

            GUI.matrix = prevMatrix;
        }

        private void DrawCollapsedBadge(float screenW, float screenH)
        {
            // Small button in top-left corner
            string label = _unreadErrorCount > 0
                ? $"DEBUG [! {_unreadErrorCount}]"
                : "DEBUG LOGS";

            GUIStyle style = _unreadErrorCount > 0 ? _badgeButtonStyle : _buttonStyle;

            if (GUI.Button(new Rect(10, 10, 110, 32), label, style))
            {
                _isExpanded = true;
                _unreadErrorCount = 0;
            }
        }

        private void DrawExpandedWindow(float screenW, float screenH)
        {
            float winW = Mathf.Min(480, screenW - 20);
            float winH = Mathf.Min(360, screenH - 20);

            Rect winRect = new Rect(10, 10, winW, winH);

            GUILayout.BeginArea(winRect, "Net & Game Debug Log", _windowStyle);

            // Telemetry Box
            GUI.backgroundColor = new Color(0.1f, 0.2f, 0.3f, 0.9f);
            GUILayout.Box(_cachedTelemetry, _telemetryStyle, GUILayout.Height(42));
            GUI.backgroundColor = Color.white;

            // Toolbar Controls
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Close", _buttonStyle, GUILayout.Width(60), GUILayout.Height(24)))
            {
                _isExpanded = false;
            }

            if (GUILayout.Button("Clear", _buttonStyle, GUILayout.Width(50), GUILayout.Height(24)))
            {
                lock (_lock)
                {
                    _logs.Clear();
                    _unreadErrorCount = 0;
                }
            }

            if (GUILayout.Button("Copy All", _buttonStyle, GUILayout.Width(70), GUILayout.Height(24)))
            {
                CopyLogsToClipboard();
            }

            GUILayout.FlexibleSpace();

            // Filters
            if (GUILayout.Toggle(_currentFilter == LogFilter.All, "All", "Button", GUILayout.Width(40))) _currentFilter = LogFilter.All;
            if (GUILayout.Toggle(_currentFilter == LogFilter.Errors, "Errors", "Button", GUILayout.Width(52))) _currentFilter = LogFilter.Errors;
            if (GUILayout.Toggle(_currentFilter == LogFilter.Network, "Net", "Button", GUILayout.Width(45))) _currentFilter = LogFilter.Network;

            GUILayout.EndHorizontal();

            // Scrollable Log List
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

            lock (_lock)
            {
                for (int i = _logs.Count - 1; i >= 0; i--)
                {
                    LogEntry entry = _logs[i];

                    // Filtering
                    if (_currentFilter == LogFilter.Errors && entry.Type != LogType.Error && entry.Type != LogType.Exception && entry.Type != LogType.Assert)
                        continue;

                    if (_currentFilter == LogFilter.Network && !entry.Message.Contains("[NET]") && !entry.Message.Contains("[SERVER") && !entry.Message.Contains("[CLIENT") && !entry.Message.Contains("FishNet") && !entry.Message.Contains("Tugboat"))
                        continue;

                    // Color code
                    Color logColor = Color.white;
                    if (entry.Type == LogType.Error || entry.Type == LogType.Exception)
                        logColor = new Color(1f, 0.4f, 0.4f);
                    else if (entry.Type == LogType.Warning)
                        logColor = new Color(1f, 0.9f, 0.4f);
                    else if (entry.Message.StartsWith("[NET]") || entry.Message.StartsWith("[SERVER") || entry.Message.StartsWith("[CLIENT"))
                        logColor = new Color(0.4f, 0.9f, 1f);

                    _logStyle.normal.textColor = logColor;

                    string display = $"[{entry.Timestamp}] {entry.Message}";
                    if (!string.IsNullOrEmpty(entry.StackTrace) && (entry.Type == LogType.Error || entry.Type == LogType.Exception))
                    {
                        display += $"\n{entry.StackTrace}";
                    }

                    GUILayout.Label(display, _logStyle);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void CopyLogsToClipboard()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== PASSING OVER IT DEBUG LOGS ===");
            sb.AppendLine($"Timestamp: {DateTime.Now}");
            sb.AppendLine(_cachedTelemetry);
            sb.AppendLine("------------------------------------");

            lock (_lock)
            {
                for (int i = 0; i < _logs.Count; i++)
                {
                    LogEntry entry = _logs[i];
                    sb.AppendLine($"[{entry.Timestamp}] [{entry.Type}] {entry.Message}");
                    if (!string.IsNullOrEmpty(entry.StackTrace) && (entry.Type == LogType.Error || entry.Type == LogType.Exception))
                    {
                        sb.AppendLine(entry.StackTrace);
                    }
                }
            }

            GUIUtility.systemCopyBuffer = sb.ToString();
            AddManualLog(LogType.Log, "[OVERLAY] All logs copied to system clipboard!");
        }

        #endregion
    }
}
