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

  
## 🏃‍♂️ Phase 6: Character Animation Setup & Network Synchronization
- [ ] **6.1 Animator Controller Setup (`PlayerAnimatorController.controller`)**
  - [ ] **Base Movement Layer**: Setup BlendTree for Idle/Walk/Run driven by `Speed` float.
  - [ ] **Air & Action States**: Add `JumpStart`, `InAir`, `Land`, `Dash`, and `Dive` states with state transitions.
  - [ ] **Upper-Body Layer (Avatar Mask)**: Create isolated upper-body layer for `HoldBomb` pose and `PassBomb` swipe gesture.
- [ ] **6.2 Dedicated Animation Controller Script (`PlayerAnimation.cs`)**
  - [ ] Create `PlayerAnimation.cs` component attached to Player prefab.
  - [ ] Read movement parameters from `PlayerController.cs` (`Speed`, `IsGrounded`, `IsDashing`, `IsDiving`, `IsJumping`).
  - [ ] Feed animator floats, bools, and triggers locally.
- [ ] **6.3 Fish-Net Network Animator Integration (`NetworkAnimator`)**
  - [ ] Attach Fish-Net `NetworkAnimator` component to Player prefab.
  - [ ] Bind component to Player `Animator`.
  - [ ] Configure synchronized animation parameters & triggers across server and observers for low-latency visual sync.

---

## 💣 Phase 7: Bomb Tagging & Passing Mechanism
- [ ] **7.1 Server-Authoritative Bomb State (`BombController.cs` / `BombPassManager.cs`)**
  - [ ] Create synchronized bomb state tracking current bomb carrier (`NetworkBehaviour` with `SyncVar` / RPCs).
  - [ ] Attach visual Bomb prefab / indicator to carrying player's hand/head socket.
- [ ] **7.2 Collision & Touch Tagging Logic**
  - [ ] Implement collision trigger / SphereCast on player (`OnTriggerEnter`).
  - [ ] Detect physical contact between Bomb Carrier and target player.
  - [ ] Enable **Dash-Tag**: Allow player to dash into another player to instantly pass the bomb.
- [ ] **7.3 Tag Immunity & Cooldown**
  - [ ] Implement **1.5-second Tag Immunity** window on receiving player to prevent instant back-tagging.
  - [ ] Broadcast visual immunity aura / feedback during cooldown.
- [ ] **7.4 Audio & Visual Tag Feedback**
  - [ ] Play tag swipe SFX and spawn particle hit effect on successful pass.
  - [ ] Update character outline / beacon light for current bomb carrier.

---

## ⏱️ Phase 8: Round Loop, Bomb Timer, Explosion & 2-Second Respawn
- [ ] **8.1 Synchronized Round & Bomb Timer (`RoundManager.cs`)**
  - [ ] Implement server-authoritative countdown timer (e.g. 30s - 45s round duration).
  - [ ] Sync remaining round time to all connected clients.
- [ ] **8.2 Bomb Explosion & Carrier Knockback**
  - [ ] When timer hits 0s, trigger bomb explosion event on server.
  - [ ] Spawn explosion particle effect and play loud explosion SFX at carrier position.
  - [ ] Put exploded player into temporary ragdoll / knockback state.
- [ ] **8.3 2-Second Respawn System**
  - [ ] Start 2-second respawn delay timer for exploded player.
  - [ ] Respawn exploded player at a random spawn point in the arena via `NetworkPlayerSpawner.cs`.
  - [ ] Server assigns bomb to a new random player to initiate the next round.

---

## 📺 Phase 9: HUD & UI Network Synchronization
- [ ] **9.1 In-Game Bomb HUD (`InGameNetworkHUD.cs` / `UIController.cs`)**
  - [ ] Display prominent countdown timer at top center of screen with color warning (Red under 5s).
  - [ ] Display "YOU HAVE THE BOMB!" localized warning header for local bomb carrier.
- [ ] **9.2 Carrier Pointer & Directional UI**
  - [ ] Render floating arrow / off-screen pointer pointing towards current bomb carrier.
- [ ] **9.3 Respawn UI Overlay**
  - [ ] Display 2-second respawn countdown prompt ("Respawning in 2... 1...") for dead player.

---

## ☁️ Phase 10: Dedicated Cloud VPS Deployment (Linux / Windows)
- [ ] Build Linux x86_64 Dedicated Server target in Unity.
- [ ] Upload build to Linux VPS (Ubuntu/Debian).
- [ ] Open UDP Port `7777` on VPS Firewall (`sudo ufw allow 7777/udp`).
- [ ] Run server executable headlessly or set up `systemd` service for 24/7 background operation.
- [ ] Connect PC and Mobile clients to VPS Public IP address.

