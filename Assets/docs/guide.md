# ⚡ AI Coding Guidelines & Unity Optimization Standards

This document defines performance standards, memory-management rules, networking standards, and code architecture guidelines for AI assistants and developers working on this Unity multiplayer codebase.

The goal is **high and consistent performance without unnecessary complexity**. Performance optimizations should prioritize measurable bottlenecks over micro-optimizations.

---

# 1. 🚀 Unity C# Performance & Memory Optimization

## A. Zero-GC Allocation in Hot Paths

### Never create unnecessary allocations in hot paths

Avoid allocating objects, arrays, collections, delegates, lambdas, closures, or strings repeatedly inside:

* `Update()`
* `FixedUpdate()`
* `LateUpdate()`
* frequently called gameplay methods
* network callbacks
* physics callbacks
* repeated timers/coroutines

```csharp
// ❌ Avoid
void Update()
{
    var players = new List<Player>();
}
```

Instead, reuse allocated collections:

```csharp
private readonly List<Player> _players = new(16);
```

and:

```csharp
_players.Clear();
```

### Important

Do not interpret this as "never use `new`."

One-time initialization allocations during scene/round loading are generally acceptable. The primary concern is **repeated runtime allocation**, especially in hot paths.

---

## B. String Allocation

Avoid repeatedly creating strings during gameplay.

```csharp
// ❌ Avoid
void Update()
{
    scoreText.text = "Score: " + score;
}
```

Prefer:

```csharp
scoreText.SetText("Score: {0}", score);
```

or update the UI only when the displayed value changes.

### Rule

> Do not continuously rebuild strings or UI text every frame when the displayed value has not changed.

---

## C. LINQ Avoidance

Do not use LINQ in performance-sensitive runtime code:

```csharp
.Where()
.Select()
.FirstOrDefault()
.OrderBy()
.ToList()
```

Prefer explicit loops in hot paths.

```csharp
for (int i = 0; i < players.Count; i++)
{
    ...
}
```

LINQ may be acceptable in editor tools, initialization code, or infrequently executed non-critical code when profiling shows no issue.

---

## D. Delegate / Lambda Allocation

Use events and delegates for event-driven architecture, but do not repeatedly create delegates or closures.

### ❌ Avoid

```csharp
void Update()
{
    SomeEvent += () => DoSomething();
}
```

### ✅ Prefer

```csharp
private void OnEnable()
{
    SomeEvent += HandleEvent;
}

private void OnDisable()
{
    SomeEvent -= HandleEvent;
}

private void HandleEvent()
{
    DoSomething();
}
```

### Rule

> Never repeatedly create lambdas, anonymous delegates, or closures in hot paths.

---

# 2. 🔄 Event-Driven Architecture

## A. Prefer Events Over Polling

Do not repeatedly check state that changes infrequently.

### ❌ Avoid

```csharp
void Update()
{
    if (gameManager.IsRoundActive)
    {
        UpdateUI();
    }
}
```

### ✅ Prefer

```csharp
gameManager.OnRoundStateChanged += HandleRoundStateChanged;
```

Use events for:

* round state changes
* bomb ownership changes
* bomb timer state changes
* player elimination
* player state changes
* match state changes
* spectator state
* UI state
* power-up activation
* game mode changes

### Rule

> If a system only needs to react when something changes, use an event/delegate instead of polling every frame.

---

## B. Event Subscription Lifecycle

Every event subscription must have a corresponding unsubscribe.

```csharp
private void OnEnable()
{
    gameManager.OnRoundStarted += HandleRoundStarted;
}

private void OnDisable()
{
    gameManager.OnRoundStarted -= HandleRoundStarted;
}
```

This is especially important for pooled objects.

Failure to unsubscribe can cause:

* memory leaks
* duplicate callbacks
* callbacks to inactive objects
* unexpected gameplay behavior

---

# 3. 🔁 Update Loop Optimization

## A. No Unnecessary Update Methods

Do not create `Update()`, `FixedUpdate()`, or `LateUpdate()` methods unless continuous processing is actually required.

### ❌ Avoid

```csharp
void Update()
{
    if (!_isActive)
        return;
}
```

If the component doesn't need to update, disable it:

```csharp
enabled = false;
```

Prefer events whenever possible.

---

## B. Use the Correct Update Loop

### `Update()`

Use for:

* input
* non-physics gameplay logic
* regular frame-based logic
* camera/input processing where appropriate

### `FixedUpdate()`

Use for:

* Rigidbody movement
* physics forces
* physics-related simulation

### `LateUpdate()`

Use primarily for:

* camera follow
* logic that must execute after normal `Update()` processing

### Rule

> Do not move physics objects from `Update()` when physics simulation is responsible for their movement.

---

# 4. 🎯 Reference Caching

## A. Component Fetching

Cache frequently accessed components.

```csharp
private Rigidbody _rigidbody;
private Animator _animator;

private void Awake()
{
    _rigidbody = GetComponent<Rigidbody>();
    _animator = GetComponent<Animator>();
}
```

Do not repeatedly call:

```csharp
GetComponent<T>()
```

inside hot paths.

---

## B. Camera Reference

Cache camera references.

```csharp
private Camera _mainCamera;

private void Awake()
{
    _mainCamera = Camera.main;
}
```

Do not repeatedly access `Camera.main` in `Update()`.

---

## C. Transform References

Cache frequently accessed transforms instead of repeatedly traversing the hierarchy.

---

## D. Runtime Object Searching

Do not use the following during gameplay hot paths:

```csharp
GameObject.Find()
GameObject.FindWithTag()
FindFirstObjectByType<T>()
FindAnyObjectByType<T>()
Transform.Find()
```

Resolve dependencies during initialization and cache them.

---

# 5. 🧮 Collection Optimization

## A. Preallocate Collection Capacity

For collections with predictable sizes:

```csharp
private readonly List<Player> _players = new(32);
private readonly Dictionary<ulong, PlayerNetwork> _playersById = new(32);
```

Avoid unnecessary collection resizing during gameplay.

---

## B. Reuse Temporary Collections

Instead of repeatedly allocating:

```csharp
var nearbyPlayers = new List<Player>();
```

reuse:

```csharp
_nearbyPlayers.Clear();
```

---

## C. Prefer Efficient Lookup Structures

For network player lookup:

```csharp
Dictionary<ulong, PlayerNetwork>
```

is preferred over repeatedly searching through the entire player list.

```csharp
if (_playersById.TryGetValue(playerId, out var player))
{
    ...
}
```

---

## D. `for` vs `foreach`

Do not ban `foreach` universally.

For performance-critical loops over arrays or `List<T>`, a traditional `for` loop is preferred when it provides measurable or predictable performance benefits.

Readable `foreach` loops are acceptable outside critical hot paths.

---

# 6. 🎬 Animator Optimization

Cache Animator parameter hashes.

```csharp
private static readonly int IsRunningHash =
    Animator.StringToHash("IsRunning");
```

Then:

```csharp
_animator.SetBool(IsRunningHash, isRunning);
```

Do not repeatedly write the same Animator value.

```csharp
if (_isRunning != isRunning)
{
    _isRunning = isRunning;
    _animator.SetBool(IsRunningHash, isRunning);
}
```

### Rule

> Cache Animator hashes and only update Animator parameters when their values actually change.

---

# 7. 🎨 Shader & Material Optimization

Cache shader property IDs:

```csharp
private static readonly int ColorId =
    Shader.PropertyToID("_Color");
```

Avoid repeatedly resolving property names by string.

Be careful when modifying materials:

```csharp
renderer.material
```

can create material instances.

Prefer shared materials where appropriate:

```csharp
renderer.sharedMaterial
```

Use `MaterialPropertyBlock` when per-renderer property changes are required without creating unique material instances.

---

# 8. 💥 Physics Optimization

## A. Non-Alloc Physics Queries

For frequently executed physics queries, prefer NonAlloc APIs where available:

```csharp
Physics.OverlapSphereNonAlloc()
Physics.RaycastNonAlloc()
```

Reuse the result buffer.

---

## B. Layer Filtering

Always use the narrowest practical `LayerMask`.

```csharp
Physics.OverlapSphereNonAlloc(
    position,
    radius,
    _results,
    _playerLayerMask
);
```

Do not query every collider and then filter irrelevant objects manually.

---

## C. Physics Collision Matrix

Disable unnecessary layer interactions in:

**Project Settings → Physics**

---

## D. Rigidbody Movement

Use physics-compatible movement:

```csharp
Rigidbody.MovePosition()
Rigidbody.MoveRotation()
```

when appropriate.

Do not directly manipulate `transform.position` on physics-controlled objects when doing so conflicts with Rigidbody simulation.

---

# 9. ♻️ Object Pooling

Frequently spawned/destroyed objects MUST use pooling.

Examples:

* VFX
* audio objects
* bomb effects
* tag effects
* elimination effects
* UI popups
* projectiles
* sticky bombs
* temporary gameplay objects

Prefer:

```csharp
UnityEngine.Pool.ObjectPool<T>
```

---

## A. Instantiate/Destroy During Active Gameplay

Avoid:

```csharp
Instantiate()
Destroy()
```

for frequently occurring gameplay objects.

Pre-warm pools during scene/round loading where appropriate.

---

## B. Pool Lifecycle

Every pooled object must completely reset its state when spawned/despawned.

Reset:

* position
* rotation
* velocity
* animation state
* particle state
* timers
* gameplay state
* visual state
* event subscriptions
* network state

Provide explicit lifecycle methods where appropriate:

```csharp
OnSpawn()
OnDespawn()
```

---

# 10. 🔊 Audio Optimization

Frequently played short sounds should use reusable `AudioSource` objects or an audio pool.

Examples:

* bomb transfer
* tag
* explosion
* countdown
* dash
* dive
* elimination
* UI effects

Avoid repeatedly instantiating and destroying temporary audio GameObjects.

---

# 11. ✨ VFX Optimization

Pool frequently spawned:

* particle systems
* explosion effects
* dash effects
* tag effects
* elimination effects
* bomb effects
* trail effects

Pool the complete GameObject rather than repeatedly creating/destroying VFX objects.

---

# 12. 🌐 Multiplayer & Networking Optimization

## A. Bandwidth Budgeting

Network traffic should be treated as a limited resource.

Do not synchronize data unless clients actually need it.

---

## B. Position & Rotation Sync

Synchronize position/rotation only when necessary.

Use:

* thresholds
* deadzones
* appropriate update rates
* interpolation

Avoid sending unchanged state.

---

## C. Network Frequency

Do not synchronize gameplay state every rendered frame.

Rendering, physics, and networking have different appropriate frequencies.

Example:

```text
Rendering: 144 FPS
Physics:    50 Hz
Network:    30–60 Hz
```

The exact values should be determined by gameplay requirements and profiling.

---

## D. Quantization

Where appropriate, reduce precision of network data.

For example, full 32-bit floats may not always be necessary for:

* positions
* rotations
* timers
* normalized values

Only apply compression when the precision loss is acceptable.

---

# 13. 📡 RPC & Network State Management

## A. Targeted RPCs

Prefer targeted RPCs when only specific clients need information.

Use broadcast/observer RPCs when all relevant clients actually need the event.

Do not broadcast unnecessarily.

---

## B. Network Variables

Avoid creating excessive independent synchronized variables.

Group related state when appropriate.

Use compact representations such as:

* bitmasks
* enums
* packed structs

when this genuinely reduces synchronization overhead without making the code unnecessarily complex.

---

## C. Synchronize Authoritative State, Not Derived State

Do not continuously synchronize values clients can calculate locally.

For example, instead of constantly synchronizing:

```text
19.8
19.7
19.6
19.5
...
```

for a bomb timer, synchronize authoritative timer state/start time and let clients calculate the displayed remaining time locally.

---

## D. Never Network UI State

Do not synchronize:

* timer text
* round text
* spectator UI
* elimination UI
* HUD state

Synchronize gameplay state and derive the UI locally.

---

# 14. 🛡️ Server Authority

The server should authoritatively control:

* bomb ownership
* bomb transfers
* eliminations
* round transitions
* round timer
* match state
* winner determination
* tag validation
* gameplay outcomes

Clients should request actions.

For example:

```text
Client:
"I attempted to tag Player X."

Server:
"Is this valid?"

Server:
"Yes → transfer bomb."
```

Do not trust clients to declare gameplay outcomes.

---

# 15. 🧊 Remote Player Interpolation

Do not simply snap remote players to every network position update:

```csharp
transform.position = networkPosition;
```

Use interpolation where appropriate.

Conceptually:

```text
Server snapshots
       ↓
Client interpolation
       ↓
Smooth remote movement
```

This can provide smoother movement without requiring excessively high network update rates.

---

# 16. 🧠 Gameplay State vs Presentation

Keep gameplay state independent from presentation.

Prefer:

```text
BombManager
      │
      └── OnBombPassed
             │
       ┌─────┼─────┬─────┐
       ↓     ↓     ↓     ↓
      UI    VFX  Audio Animation
```

instead of tightly coupling:

```text
BombManager → UI
BombManager → Audio
BombManager → VFX
BombManager → Animation
```

Gameplay systems should publish state changes.

Presentation systems should subscribe.

---

# 17. 🖥️ UI Optimization

UI should be event-driven whenever possible.

### ❌ Avoid

```csharp
void Update()
{
    timerText.text = bombTimer.ToString();
}
```

### Prefer

```csharp
bombManager.OnTimerChanged += HandleTimerChanged;
```

Only update the UI when the displayed value changes.

For example, if the UI displays whole seconds, there is no reason to redraw it every frame.

```csharp
int seconds = Mathf.CeilToInt(timer);

if (seconds != _lastDisplayedSeconds)
{
    _lastDisplayedSeconds = seconds;
    _timerText.SetText("{0}", seconds);
}
```

---

# 18. 🧵 Jobs & Burst

Use Unity Jobs/Burst when profiling identifies substantial CPU workloads that can benefit from parallel processing.

Potential candidates:

* large batches of calculations
* procedural generation
* large-scale spatial processing
* expensive simulation
* large numbers of independent calculations

Do NOT introduce Jobs/Burst merely because it sounds faster.

Prefer normal C# when the workload is small.

---

# 19. 📝 Logging Optimization

Avoid high-frequency logging during gameplay.

### ❌ Avoid

```csharp
void Update()
{
    Debug.Log("Player updating");
}
```

Avoid excessive:

```csharp
Debug.Log()
Debug.LogWarning()
Debug.LogError()
```

inside hot paths.

Use conditional/debug logging and disable verbose logging in release builds.

---

# 20. 🧹 Garbage Collection Awareness

Avoid generating garbage repeatedly during gameplay.

Common sources to watch:

* LINQ
* boxing
* string formatting
* closures
* temporary collections
* repeated allocations
* unnecessary arrays
* coroutine allocations where applicable
* repeated delegate creation
* APIs that return newly allocated collections

Use Unity's Profiler/Memory Profiler to identify actual GC sources rather than assuming every allocation is harmful.

---

# 21. 🧱 Structs vs Classes

Use structs for small value-like data that is frequently processed.

Example:

```csharp
public struct PlayerState
{
    public Vector3 Position;
    public bool IsEliminated;
    public bool HasBomb;
}
```

Do not convert everything into structs.

Large structs can become expensive to copy and structs are inappropriate when reference semantics are required.

---

# 22. 🧪 Profile Before Optimizing

Performance decisions should be evidence-based.

Use appropriate profiling tools such as:

* Unity Profiler
* Memory Profiler
* Frame Debugger
* rendering statistics
* network profiling tools

Measure:

* CPU usage
* GPU usage
* GC allocations
* memory usage
* draw calls
* network bandwidth
* network frequency
* physics cost

### Rule

> Never add significant complexity solely because a technique is considered "optimized." Confirm that the optimization addresses an actual bottleneck.

---

# 23. 📈 Performance Regression Testing

When making major performance changes:

1. Profile before the change.
2. Implement the change.
3. Profile again.
4. Verify that performance actually improved.
5. Confirm that functionality did not regress.

Do not assume an optimization worked simply because the code looks more efficient.

---

# 24. 🎯 Optimization Priority

Prioritize optimization in this order:

1. Network bandwidth and synchronization
2. Garbage collection and repeated allocations
3. Physics cost
4. Unnecessary `Update()` polling
5. Rendering/VFX
6. CPU-heavy gameplay systems
7. Memory usage
8. Micro-optimizations

Do not sacrifice architecture or readability to optimize code that executes only once per scene or round.

---

# 25. 🚫 Avoid Cargo-Cult Optimization

The following statements should NOT be treated as universal rules:

* "`new` is always bad."
* "`foreach` is always bad."
* "Delegates are always slow."
* "Classes are always slower than structs."
* "Every `GetComponent()` is expensive."
* "Everything must use Jobs/Burst."
* "Everything must use NonAlloc."
* "Every function needs to be optimized."

Instead:

> Optimize based on execution frequency, allocation behavior, data size, scale, and measured performance impact.

---

# 26. 🏗️ Code Architecture & AI Comprehension

## A. Explicit Architecture & Guard Clauses

Use guard clauses to flatten control flow.

```csharp
void OnTagPlayer(Player target)
{
    if (target == null) return;
    if (!isServer) return;
    if (isCooldown) return;

    ExecuteTag(target);
}
```

---

## B. Single Responsibility Principle

Keep components focused.

Example architecture:

```text
PlayerController.cs
    → locomotion, jump, dash, dive physics

PlayerNetwork.cs
    → network state, RPC routing

BombManager.cs
    → bomb timer, ownership, transfer, explosion

RoundManager.cs
    → round progression and elimination thresholds

GameManager.cs
    → match-level state

UIController.cs
    → HUD, timer display, touch controls

AudioManager.cs
    → audio playback

VFXManager.cs
    → pooled visual effects
```

Avoid creating giant "God classes."

---

# 27. 🏷️ Naming Conventions

### Public / Serialized Fields

Use `PascalCase` where project conventions require it.

### Private Fields

Use:

```csharp
_camelCase
```

### Local Variables / Parameters

Use:

```csharp
camelCase
```

### Classes / Methods / Properties

Use:

```csharp
PascalCase
```

Keep naming consistent across the entire project.

---

# 28. 📚 XML Documentation

Add XML documentation to public interfaces and important network/gameplay methods.

```csharp
/// <summary>
/// Authoritatively transfers the active bomb from the current holder
/// to the target player.
/// Must be invoked on the server.
/// </summary>
public void TransferBomb(ulong targetPlayerId)
{
    ...
}
```

Document:

* public interfaces
* network RPCs
* important gameplay state
* custom state structures
* non-obvious performance constraints

---

# 29. 🧭 Dependency Management

Prefer explicit dependencies over hidden scene searches.

Good:

```csharp
public void Initialize(
    BombManager bombManager,
    RoundManager roundManager)
{
    _bombManager = bombManager;
    _roundManager = roundManager;
}
```

Avoid systems secretly finding everything they need at runtime.

This makes the code easier for both humans and AI assistants to reason about.

---

# 30. ⚠️ AI Implementation Rules

When modifying this codebase, AI assistants must:

1. Preserve existing architecture unless a change is explicitly required.
2. Avoid introducing unnecessary allocations.
3. Avoid adding `Update()` polling when an event-driven solution is appropriate.
4. Use events/delegates for state changes that do not require per-frame evaluation.
5. Properly subscribe/unsubscribe events.
6. Cache frequently accessed components and references.
7. Use object pooling for frequently created/destroyed gameplay objects.
8. Avoid unnecessary network synchronization.
9. Keep authoritative gameplay logic on the server.
10. Avoid synchronizing UI/presentation state.
11. Reuse collections where appropriate.
12. Use appropriate physics LayerMasks.
13. Avoid unnecessary logging.
14. Avoid unnecessary `Find*()` calls.
15. Prefer readable code unless profiling justifies additional complexity.
16. Do not introduce Jobs/Burst or other advanced optimization techniques without a legitimate workload.
17. Preserve existing public APIs unless there is a strong reason to change them.
18. Before making a major optimization, consider its actual execution frequency and measurable impact.
19. When modifying pooled objects, ensure their complete state is reset.
20. When modifying networked gameplay, consider bandwidth, authority, prediction/interpolation, and synchronization frequency.

---

# ⚡ Quick AI Performance Cheatsheet

1. **No unnecessary allocations in hot paths.**
2. **No LINQ in runtime hot paths.**
3. **No `GetComponent()` in hot paths.**
4. **No `Find*()` calls during gameplay.**
5. **Cache frequently used references.**
6. **Use NonAlloc APIs where appropriate.**
7. **Pool frequently spawned/destroyed objects.**
8. **No unnecessary `Update()` loops.**
9. **Prefer events over polling for infrequent state changes.**
10. **Do not repeatedly create lambdas/closures/delegates.**
11. **Always unsubscribe event handlers appropriately.**
12. **Update UI only when displayed state changes.**
13. **Cache Animator/Shader property IDs.**
14. **Use narrow LayerMasks for physics queries.**
15. **Reuse temporary collections.**
16. **Preallocate collection capacity where practical.**
17. **Avoid unnecessary Animator parameter writes.**
18. **Avoid high-frequency logging.**
19. **Separate rendering, physics, and network frequencies.**
20. **Synchronize authoritative state, not derived state.**
21. **Use targeted RPCs when broadcast is unnecessary.**
22. **Never network UI/presentation state.**
23. **Use interpolation for remote movement where appropriate.**
24. **Keep server authoritative over gameplay outcomes.**
25. **Use Jobs/Burst only for measured CPU bottlenecks.**
26. **Profile before significant optimization.**
27. **Prioritize hot paths over one-time initialization.**
28. **Completely reset pooled objects.**
29. **Keep gameplay state decoupled from presentation.**
30. **Prefer simple, measurable optimizations over cargo-cult optimization.**

---

# 🏆 Core Principle

> **Write the simplest architecture that meets the game's requirements, then optimize the parts that profiling proves are expensive.**

Performance is important, but unnecessary complexity is also a performance and maintenance problem.

The objective is not to make every line of C# theoretically optimal.

The objective is to maintain:

**High FPS + Low GC + Low CPU usage + Low bandwidth + Stable memory usage + Clean architecture + Maintainable code.**
