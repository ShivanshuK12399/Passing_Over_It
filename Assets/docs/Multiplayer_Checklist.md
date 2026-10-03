# 🎮 Multiplayer Implementation Checklist (Dedicated Server Architecture)

**Project:** Passing Over It  
**Goal:** Enable full multi-device play (PC ↔ Mobile, Mobile ↔ Mobile, PC ↔ PC) connecting to a **Dedicated Cloud Server** with all movement, bomb tagging, timers, round flow, and HUD synced across devices.  
**Networking Architecture:** Unity 6 + Fish-Net (Tugboat UDP Transport) + Dedicated Server (Local PC / Linux & Windows VPS)

---

## 📋 Phase 1: Package Setup & Transport Configuration
- [x] **1.1 Fish-Net Package Installation**
  - [x] Install **Fish-Net: Networking Evolution** via Unity Asset Store / UPM.
  - [x] Configure default `Tugboat` transport component on `NetworkManager` (Default Port: `7777` UDP).

---

## 🌐 Phase 2: Dedicated Server Bootstrapping & Direct IP Flow
- [x] **2.1 Command-Line Argument Parser (`ServerCommandLineArgs.cs`)**
  - [x] Implement parser for `-port <int>`, `-ip <string>`, `-maxplayers <int>`.
  - [x] Automatically detect `-batchmode`, `-dedicated`, or `-server` launch flags.
- [x] **2.2 Dedicated Server Manager (`DedicatedServerManager.cs`)**
  - [x] Auto-bootstrap server when launched with CLI server flags.
  - [x] Configure Tugboat port and listen socket.
  - [x] Auto-load Arena scene (Build Index 1) on server start so clients can connect immediately.
  - [x] Implement `StartClient(ip, port)` for direct socket connection.
- [x] **2.3 Menu UI & Connection Interface (`MultiplayerMenuUI.cs`)**
  - [x] IP Address Input Field (Default: `127.0.0.1`).
  - [x] Port Input Field (Default: `7777`).
  - [x] **Quick Connect Localhost** button for one-click local PC testing.
  - [x] **Start Local Server** button for testing inside Editor/Standalone.
  - [x] Connection Status indicator (Connecting, Connected, Server Started, Disconnected, Error).

---

## 🏃 Phase 3: Player Synchronization (`PlayerController.cs`)
- [x] **3.1 `NetworkObject` & `NetworkTransform` Integration**
  - [x] Attach Fish-Net `NetworkObject` to Player Prefab.
  - [x] Sync position and rotation smoothly via `NetworkTransform`.
- [x] **3.2 Input & Ownership Isolation**
  - [x] Guard movement and input with `if (!IsOwner) return;`.
  - [x] Ensure Touch (Mobile) and Keyboard (PC) inputs only process for the local player instance.
- [x] **3.3 Camera Isolation**
  - [x] Bind Cinemachine camera target exclusively to local player (`if (IsOwner)`).
- [x] **3.4 Action Synchronization (Dash & Dive)**
  - [x] Send `[ServerRpc]` when local player triggers Dash or Dive.
  - [x] Broadcast visuals/movement state across clients.

---

## 🤝 Phase 4: Player Joining, Spawning & Lobby Connection Setup
- [x] **4.1 Player Connection & Spawning (`NetworkPlayerSpawner.cs`)**
  - [x] Handle server-side client connection events (`OnClientConnectionState`).
  - [x] Spawn player prefabs at designated spawn points upon joining.
  - [x] Synchronize player names, IDs, and connection status across all clients.
- [x] **4.2 Lobby / Room Connection & Sync**
  - [x] Track connected players list on the server.
  - [x] Handle player disconnect cleanups gracefully.

---

## 🧪 Phase 5: Local PC Testing & VPS Deployment
- [x] **5.1 Local PC Dedicated Server Testing**
  - [x] Build Windows Dedicated Server executable (or run standalone build with `-batchmode -nographics -port 7777`).
  - [x] Open Unity Editor / Standalone, click **Quick Connect Localhost (`127.0.0.1:7777`)**.
  - [x] Verify player spawning, position sync, and movement across client instances.
- [x] **5.2 Standalone Dual Client Test**
  - [x] Run standalone client builds connected to the local server process.
  - [x] Verify latency stability and connection handling under local socket conditions.

  
## Phase 6 Cloud VPS Deployment (Linux / Windows)
  - [ ] Build Linux x86_64 Dedicated Server target in Unity.
  - [ ] Upload build to Linux VPS (Ubuntu/Debian).
  - [ ] Open UDP Port `7777` on VPS Firewall (`sudo ufw allow 7777/udp`).
  - [ ] Run server executable headlessly or set up `systemd` service for 24/7 background operation.
  - [ ] Connect PC and Mobile clients to VPS Public IP address.
