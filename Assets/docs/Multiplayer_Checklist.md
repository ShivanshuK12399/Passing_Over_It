# 🎮 Multiplayer Implementation Checklist (2-Device Synchronization)

**Project:** Passing Over It  
**Goal:** Enable full 2-device multiplayer play (PC ↔ Mobile, Mobile ↔ Mobile, PC ↔ PC) over internet/Wi-Fi with all player movement, bomb tagging, timers, round flow, and UI synced across devices.  
**Networking Architecture:** Unity 6 + Fish-Net Networking + Unity Gaming Services (UGS Relay, Lobby, Auth)

---

## 📋 Phase 1: UGS Setup & Package Installation
- [x] **1.1 Unity Gaming Services (UGS) Setup**
  - [x] Open Unity Dashboard ([dashboard.unity3d.com](https://dashboard.unity3d.com)) and select project.
  - [x] Link Unity Project ID in Unity Editor (`Edit -> Project Settings -> Services`).
  - [x] Enable **Authentication** (Anonymous Auth).
  - [x] Enable **Relay** service (Free tier up to 50 Concurrent Users).
  - [x] Enable **Lobby** service (Free tier up to 250 Concurrent Users).
- [x] **1.2 Install Required Packages**
  - [x] Install **Fish-Net: Networking Evolution** via Unity Asset Store / UPM.
  - [x] Install Unity Services SDKs via Package Manager (`com.unity.services.core`, `com.unity.services.authentication`, `com.unity.services.relay`, `com.unity.services.lobby`).

---

## 🌐 Phase 2: Host & Client Connection Flow (2-Device Connectivity)
- [x] **2.1 UGS Authentication & Relay Manager (`UGSManager.cs`)**
  - [x] Initialize `UnityServices.InitializeAsync()`.
  - [x] Authenticate player anonymously via `AuthenticationService.Instance.SignInAnonymouslyAsync()`.
  - [x] Implement `CreateRelayHostAsync(maxPlayers)` to get Relay Join Code and configure Fish-Net Transport for Host.
  - [x] Implement `JoinRelayAsync(joinCode)` to connect Fish-Net Client to Host Relay server.
- [x] **2.2 Lobby Manager (`LobbyManager.cs`)**
  - [x] Create lobby with room code, max players, and lobby name.
  - [x] Query and display list of public available lobbies.
  - [x] Store Relay Join Code in Lobby Metadata so joining clients connect automatically.
- [x] **2.3 Multiplayer Main Menu & Connection UI**
  - [x] Create UI Panel in Main Menu with:
    - Host Game Button (Generates Lobby + Join Code).
    - Join Game Input Field (Type 6-character room code).
    - Public Lobby List View.
    - Connection Status Indicator (Connecting, Authenticated, Connected, Error).

---

## 🏃 Phase 3: Player Synchronization (`PlayerController.cs` Refactoring)
- [x] **3.1 Convert `PlayerController` to `NetworkBehaviour`**
  - [x] Attach Fish-Net `NetworkObject` to Player Prefab.
  - [x] Attach `NetworkTransform` to sync position and rotation smoothly across devices.
  - [x] Change `PlayerController` base class from `MonoBehaviour` to `FishNet.Object.NetworkBehaviour`.
- [x] **3.2 Input & Ownership Isolation**
  - [x] Guard input reading and movement execution with `if (!IsOwner) return;`.
  - [x] Ensure Touch Controls (Mobile) and Keyboard (PC) only send inputs for the local player instance.
- [x] **3.3 Camera Isolation & Local Target Binding**
  - [x] Ensure `freeLookCam` (Cinemachine) target proxy only binds to local player transform (`if (IsOwner)`).
  - [x] Prevent remote player spawns from overwriting or hijacking local device camera.
- [x] **3.4 Action Synchronization (Dash & Dive)**
  - [x] Send `[ServerRpc]` when local player presses **Dash** or **Dive**.
  - [x] Broadcast `[ObserversRpc]` or sync network state so remote clients trigger dash/dive mesh rotation, scale, and velocity visuals.
- [x] **3.5 Animation Synchronization**
  - [x] Attach `NetworkAnimator` component to sync animation states (Idle, Run, Jump, Dash, Dive) across devices without lag.

---

## 💣 Phase 4: Bomb Tag & Elimination Synchronization
- [x] **4.1 Authoritative Bomb Manager (`NetworkBombManager.cs`)**
  - [x] Server-authoritative `SyncVar<ulong>` (or `NetworkVariable`) for `CurrentBombHolderId`.
  - [x] Parent Bomb 3D model to current holder's transform across all connected devices.
  - [x] Server-authoritative timer: Host runs countdown, clients calculate remaining time locally via synced network time.
- [x] **4.2 Tag / Pass Collision Detection**
  - [x] Detect collision between Tagger and Runner authoritatively on Host/Server.
  - [x] Server validates tag -> updates `CurrentBombHolderId` -> fires `[ObserversRpc]` for Tag VFX & Sound.
- [x] **4.3 Elimination & Spectator Mode**
  - [x] On timer reaching zero, Server triggers explosion at holder location.
  - [x] Server sets player state to `IsEliminated = true`.
  - [x] Eliminated client's device disables local player colliders/movement and switches to Spectator Free Camera.

---

## 🏆 Phase 5: Round System & UI Synchronization
- [x] **5.1 Round State Machine (`NetworkMatchManager.cs`)**
  - [x] Synchronize game states across all devices: `WaitingForPlayers`, `RoundStart`, `RoundActive`, `RoundEnd`, `GameOver`.
  - [x] Round 1, 2, 3 thresholds handled authoritatively by Host.
  - [x] Randomly re-assign bomb to surviving player at start of each round on all screens.
  - [x] Announce match winner on all device screens upon final elimination.
- [x] **5.2 Synchronized In-Game HUD**
  - [x] Sync HUD UI: Bomb Countdown text, Active Players remaining count, Current Round number.
  - [x] Display Player Nameplates and Bomb Holder Indicator icon over avatars in world space.

---

## 🧪 Phase 6: 2-Device Testing & Verification
- [ ] **6.1 In-Editor Dual Instance Testing (ParrelSync)**
  - [ ] Install ParrelSync asset to launch 2 Unity Editor instances on one machine.
  - [ ] Host on Instance 1 -> Join via Room Code on Instance 2.
  - [ ] Test position sync, dash/dive state, bomb tag transfer, and spectator mode.
- [ ] **6.2 Cross-Device Build Testing (PC ↔ Mobile / Mobile ↔ Mobile)**
  - [ ] Build Android APK + PC Executable (or 2 Android devices).
  - [ ] Connect Device 1 (Mobile / Wi-Fi A) as Host and Device 2 (PC / Mobile Data) as Client.
  - [ ] Confirm UGS Relay enables seamless connection across different networks without port forwarding.
- [ ] **6.3 Network Latency & Smoothing Calibration**
  - [ ] Test movement under simulated 100ms–200ms latency.
  - [ ] Calibrate Fish-Net prediction and NetworkTransform send rate (30 Hz – 60 Hz) for lag-free motion.
