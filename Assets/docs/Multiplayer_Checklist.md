# 🎮 Multiplayer Implementation Checklist (2-Device Synchronization)

**Project:** Passing Over It  
**Goal:** Enable full 2-device multiplayer play (PC ↔ Mobile, Mobile ↔ Mobile, PC ↔ PC) over internet/Wi-Fi with all player movement, bomb tagging, timers, round flow, and UI synced across devices.  
**Networking Architecture:** Unity 6 + Fish-Net Networking + Unity Gaming Services (UGS Relay, Lobby, Auth)

---

## 📋 Phase 1: UGS Setup & Package Installation
- [ ] **1.1 Unity Gaming Services (UGS) Setup**
  - [ ] Open Unity Dashboard ([dashboard.unity3d.com](https://dashboard.unity3d.com)) and select project.
  - [ ] Link Unity Project ID in Unity Editor (`Edit -> Project Settings -> Services`).
  - [ ] Enable **Authentication** (Anonymous Auth).
  - [ ] Enable **Relay** service (Free tier up to 50 Concurrent Users).
  - [ ] Enable **Lobby** service (Free tier up to 250 Concurrent Users).
- [ ] **1.2 Install Required Packages**
  - [ ] Install **Fish-Net: Networking Evolution** via Unity Asset Store / UPM.
  - [ ] Install Unity Services SDKs via Package Manager (`com.unity.services.core`, `com.unity.services.authentication`, `com.unity.services.relay`, `com.unity.services.lobby`).

---

## 🌐 Phase 2: Host & Client Connection Flow (2-Device Connectivity)
- [ ] **2.1 UGS Authentication & Relay Manager (`UGSManager.cs`)**
  - [ ] Initialize `UnityServices.InitializeAsync()`.
  - [ ] Authenticate player anonymously via `AuthenticationService.Instance.SignInAnonymouslyAsync()`.
  - [ ] Implement `CreateRelayHostAsync(maxPlayers)` to get Relay Join Code and configure Fish-Net Transport for Host.
  - [ ] Implement `JoinRelayAsync(joinCode)` to connect Fish-Net Client to Host Relay server.
- [ ] **2.2 Lobby Manager (`LobbyController.cs`)**
  - [ ] Create lobby with room code, max players, and lobby name.
  - [ ] Query and display list of public available lobbies.
  - [ ] Store Relay Join Code in Lobby Metadata so joining clients connect automatically.
- [ ] **2.3 Multiplayer Main Menu & Connection UI**
  - [ ] Create UI Panel in Main Menu with:
    - Host Game Button (Generates Lobby + Join Code).
    - Join Game Input Field (Type 6-character room code).
    - Public Lobby List View.
    - Connection Status Indicator (Connecting, Authenticated, Connected, Error).

---

## 🏃 Phase 3: Player Synchronization (`PlayerController.cs` Refactoring)
- [ ] **3.1 Convert `PlayerController` to `NetworkBehaviour`**
  - [ ] Attach Fish-Net `NetworkObject` to Player Prefab.
  - [ ] Attach `NetworkTransform` to sync position and rotation smoothly across devices.
  - [ ] Change `PlayerController` base class from `MonoBehaviour` to `FishNet.Object.NetworkBehaviour`.
- [ ] **3.2 Input & Ownership Isolation**
  - [ ] Guard input reading and movement execution with `if (!IsOwner) return;`.
  - [ ] Ensure Touch Controls (Mobile) and Keyboard (PC) only send inputs for the local player instance.
- [ ] **3.3 Camera Isolation & Local Target Binding**
  - [ ] Ensure `freeLookCam` (Cinemachine) target proxy only binds to local player transform (`if (IsOwner)`).
  - [ ] Prevent remote player spawns from overwriting or hijacking local device camera.
- [ ] **3.4 Action Synchronization (Dash & Dive)**
  - [ ] Send `[ServerRpc]` when local player presses **Dash** or **Dive**.
  - [ ] Broadcast `[ObserversRpc]` or sync network state so remote clients trigger dash/dive mesh rotation, scale, and velocity visuals.
- [ ] **3.5 Animation Synchronization**
  - [ ] Attach `NetworkAnimator` component to sync animation states (Idle, Run, Jump, Dash, Dive) across devices without lag.

---

## 💣 Phase 4: Bomb Tag & Elimination Synchronization
- [ ] **4.1 Authoritative Bomb Manager (`NetworkBombManager.cs`)**
  - [ ] Server-authoritative `SyncVar<ulong>` (or `NetworkVariable`) for `CurrentBombHolderId`.
  - [ ] Parent Bomb 3D model to current holder's transform across all connected devices.
  - [ ] Server-authoritative timer: Host runs countdown, clients calculate remaining time locally via synced network time.
- [ ] **4.2 Tag / Pass Collision Detection**
  - [ ] Detect collision between Tagger and Runner authoritatively on Host/Server.
  - [ ] Server validates tag -> updates `CurrentBombHolderId` -> fires `[ObserversRpc]` for Tag VFX & Sound.
- [ ] **4.3 Elimination & Spectator Mode**
  - [ ] On timer reaching zero, Server triggers explosion at holder location.
  - [ ] Server sets player state to `IsEliminated = true`.
  - [ ] Eliminated client's device disables local player colliders/movement and switches to Spectator Free Camera.

---

## 🏆 Phase 5: Round System & UI Synchronization
- [ ] **5.1 Round State Machine (`NetworkMatchManager.cs`)**
  - [ ] Synchronize game states across all devices: `WaitingForPlayers`, `RoundStart`, `RoundActive`, `RoundEnd`, `GameOver`.
  - [ ] Round 1, 2, 3 thresholds handled authoritatively by Host.
  - [ ] Randomly re-assign bomb to surviving player at start of each round on all screens.
  - [ ] Announce match winner on all device screens upon final elimination.
- [ ] **5.2 Synchronized In-Game HUD**
  - [ ] Sync HUD UI: Bomb Countdown text, Active Players remaining count, Current Round number.
  - [ ] Display Player Nameplates and Bomb Holder Indicator icon over avatars in world space.

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
