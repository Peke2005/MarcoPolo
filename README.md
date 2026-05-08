# Marco Polo / Frente Partido

Top-down 2D online shooter for PC. Supports two game modes:

- **1v1 Rounds**: best-of-9, kill ends round, sudden death (cover destroy) on time-out.
- **Deathmatch**: 2–10 players, 10 min timer, first to 20 kills (or top score on time-out).

Built in **Unity 2022.3.62f1** with **Netcode for GameObjects (NGO)**, **Unity Relay**, **Unity Lobby**, the **new Input System**, and **TextMeshPro**. Auth/profile stats run on a self-hosted Node + Postgres backend (Docker) reachable over a Radmin VPN.

---

## 1. Project layout

```
MarcoPolo/
├── Assets/
│   ├── Scripts/
│   │   ├── Abilities/        Dash / Shield / Mine + AbilityController
│   │   ├── Auth/             Auth login UI + REST client
│   │   ├── Combat/           WeaponController, GrenadeController, Projectile, DamageDealer
│   │   ├── Core/             SceneFlowController, GameConfig, GameplayVisualNormalizer, ProfileStats
│   │   ├── Data/             ScriptableObjects (BalanceTuning, MapDefinition, AbilityDefinition, WeaponData) + enums
│   │   ├── Editor/           FrentePartidoSetup (scene factory), StandaloneSmokeBuild
│   │   ├── Match/            MatchManager, RoundManager, SuddenDeathController, BeaconCaptureController
│   │   ├── Networking/       NetworkSessionManager, RelayConnectionManager, LobbyManager,
│   │   │                     PlayerSpawnManager, NetworkGameState, ClientNetworkTransform
│   │   ├── Pickups/          AmmoPickup, ArmorPickup, HealthPickup, PickupSpawner
│   │   ├── Player/           PlayerInputReader, PlayerMotor2D, PlayerHealth, PlayerPresentation,
│   │   │                     PlayerAimController, PlayerStateController, HitFlashController
│   │   └── UI/               MainMenuUI, LobbyUI, HUDController, ResultsUI, RoundTimerUI, CooldownWidget
│   ├── Resources/NetworkPrefabs/   Player, Grenade, Mine, Pickup_*  (registered at runtime)
│   ├── ScriptableObjects/    Balance/MainBalance.asset, Abilities/*, Maps/*, Weapons/Rifle_Standard.asset
│   ├── Scenes/               00_Boot, 01_Auth, 02_MainMenu, 03_Lobby, 04_Game, 05_PostMatch
│   └── TextMesh Pro/         LiberationSans SDF font + shaders
├── Backend/                  Node + Postgres auth/profile API (Docker)
├── Builds/Release/           Player build + zip — TRACKED in git so friends `git pull` to update
├── Tools/                    Build / Radmin helper PowerShell scripts
├── HOST_RADMIN.bat           One-click host: starts auth backend, launches game
├── CLIENT_RADMIN.bat         One-click client: launches game
└── RADMIN_RELEASE.md         Original radmin/release notes
```

Each script domain is its own assembly (asmdef) so compile times stay short and circular references are catch-able.

---

## 2. Game modes

### 1v1 Rounds (`GameMode.Rounds1v1`)
- 2 players, host = Blue (slot 0), client = Red (slot 1).
- Round duration **60 s**, intro **3 s**, round-end delay **4 s**.
- Kill ends the round; the killer wins the round.
- If the timer expires with both players alive, **sudden death** triggers: every `Cover_*` and `Decor_*` object is destroyed by `SuddenDeathController.BreakSuddenDeathCover` so neither player has cover. Round still ends only on a kill (60 s safety cap).
- First to **5 round wins** takes the match (`roundsToWin = 5`, `maxRounds = 9`).

### Deathmatch (`GameMode.Deathmatch`)
- 2–10 players. Lobby grid grows from 2-card row to 2×5 grid based on `_selectedMode`.
- Match duration **10 min** (`deathmatchDuration = 600`), **20 kills** to win (`deathmatchKillsToWin = 20`).
- Respawns are random: `PlayerSpawnManager.GetRandomDeathmatchSpawn` picks from `MapDefinition.deathmatchSpawnPoints` biased toward the spawn furthest from any living opponent (60% best, 40% second-best).
- Bigger arena (44×26), bounds switched at runtime via `RuntimeMatchSettings.BoundsMin/Max`.
- **Each kill refills the killer's grenade** — `MatchManager.RegisterKillServer` calls `RefillKillerGrenadeServer` that bumps `WeaponController.GrenadesRemaining` to 1 on the killer's player object.
- Central plaza: tinted floor + glow + marker, fully open from every direction (no walls).
- Live scoreboard top-left lists the top 5 by kills under the header `DM`.
- On time-up, `MatchManager.FinishDeathmatchByScoreServer` declares the leader.

The match-end overlay (`HUDController.ShowBigOverlay`) reads `MatchManager.FinalP1Score` / `FinalP2Score` cached when `NotifyMatchEndClientRpc` arrives, so client and host can't disagree on the final number even if a NetworkVariable update raced the RPC.

---

## 3. Networking architecture

### Stack
- **Netcode for GameObjects 1.11** — host-authoritative server-RPC + ClientRpc model.
- **Unity Relay** — NAT-traverse via Unity's relay servers; the lobby code is a Relay join code.
- **Unity Lobby** — used to advertise the room metadata (the actual gameplay link is the Relay allocation).
- **NGO `UnityTransport`** wrapping the Relay allocation.

### Player transform: owner-authoritative
`Assets/Scripts/Networking/ClientNetworkTransform.cs` overrides `OnIsServerAuthoritative()` to return `false`. The Player prefab references this via the GUID `9b4c8a2e7d1f4a90b9876a14b3c77100`. Result: each owner writes their own transform and the server replicates to others. Without this, clients rubber-banded one round-trip behind the server.

Movement abilities that need to teleport the local player (Dash) run **on the owner side first** (`AbilityController.HandleAbilityInput`) before sending the cooldown RPC, otherwise the server's `motor.TeleportTo` would never reach the client.

### Lobby state
Custom NGO named messages:

| Message ID         | Direction        | Purpose                                                |
|--------------------|------------------|--------------------------------------------------------|
| `FP_LOBBY_UPDATE`  | client → server  | "Here's my name/ability/ready state"                   |
| `FP_LOBBY_STATE`   | server → clients | Authoritative roster + selected `GameMode` snapshot    |

Joiners re-publish their state at +0.4 s, +1.0 s, +2.0 s, +4.0 s after `LobbyUI.Start` (`RepublishBootstrap` coroutine) because the very first send sometimes fired before NGO's `IsListening` flag flipped, leaving the host with the placeholder name.

### Spawn flow
1. `PlayerSpawnManager` (server-only) listens to `OnClientConnected`.
2. Spawns the Player prefab at the slot point (`spawnPointA` for slot 0, `spawnPointB` for slot 1, or curated DM points).
3. Calls `SpawnAsPlayerObject` then issues `RespawnPlayerClientRpc` so the owner snaps to the spawn position locally — required because owner-auth NT would otherwise overwrite the server's position with the prefab's local origin.
4. When 2+ players are present, `NetworkGameState.AssignPlayerSlots` sets `Player1ClientId` / `Player2ClientId`.
5. `RoundManager.Update` (server) auto-discovers both `PlayerHealth` instances and calls `RegisterPlayers` so death events route correctly.

---

## 4. Auth + profile backend

The backend lives in `Backend/`. It is a Node 20 + Express server backed by Postgres 16, packaged as Docker.

### Hosting

| Environment        | URL                              | Notes                                         |
|--------------------|----------------------------------|-----------------------------------------------|
| Primary (Radmin)   | `http://26.17.117.206:3001`      | baked into the build via `GameConfig.DEFAULT_AUTH_BASE_URL` |
| Fallback (Radmin)  | `http://26.234.30.190:3001`      | tried automatically if primary times out      |

The host machine is whichever PC runs `HOST_RADMIN.bat`. Both peers must be connected to the same **Radmin VPN** so the `26.x.x.x` private IP is reachable.

To start the backend manually:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\StartAuthBackendRadmin.ps1 -RadminIp 26.17.117.206 -Port 3001
```

The script:
1. Detects whether `Backend/.env` has a real `DATABASE_URL` (Supabase mode) or falls back to the local Postgres in `Backend/docker-compose.yml`.
2. Adds a Windows Firewall rule for TCP `3001` (run once as admin).
3. `docker compose up -d` starts the API container (and the local DB if not using Supabase).

Health check returns `{"status":"ok"}` from `/health`.

### REST endpoints

All under the base URL above.

| Method | Path               | Auth     | Body / params                                     | Purpose                                            |
|--------|--------------------|----------|---------------------------------------------------|----------------------------------------------------|
| GET    | `/health`          | none     | —                                                 | Liveness probe. Returns `{ status: "ok" }`.        |
| POST   | `/auth/register`   | none     | `{ username, password }`                          | Creates a user. Returns `{ token, user }`.         |
| POST   | `/auth/login`      | none     | `{ username, password }`                          | Logs in. Returns `{ token, user }`.                |
| GET    | `/auth/verify`     | Bearer   | —                                                 | Verifies the JWT and returns the user payload.     |
| GET    | `/profile/stats`   | Bearer   | —                                                 | Returns matches played / wins / losses / win rate. |
| POST   | `/profile/match`   | Bearer   | `{ won: bool }`                                   | Records the result of a finished match.            |

`requireAuth` middleware verifies a Bearer JWT signed with `JWT_SECRET` (configured via env in `docker-compose.yml`).

### Data model (Postgres)
Initialized by `Backend/init.sql`:

- `users(id, username UNIQUE, password_hash, created_at)` — bcrypt password hashes.
- `matches(id, user_id FK users, won BOOL, played_at)` — append-only match log.

`/profile/stats` aggregates over `matches` for the authenticated user.

### Game-side integration
- `Assets/Scripts/Auth/AuthService.cs` — REST client, exposes `RegisterAsync`, `LoginAsync`, `VerifyTokenAsync`.
- `Assets/Scripts/Core/ProfileStats.cs` — calls `/profile/stats` and `/profile/match`. The HUD invokes `ProfileStats.RecordMatchAsync(localWon)` exactly once per match-end (guarded by `_profileMatchRecorded` so a re-fired `OnMatchWon` doesn't double-count).

---

## 5. Client launch flow

```
┌────────────┐   ┌─────────────┐   ┌────────────────┐   ┌──────────┐   ┌──────────┐
│ 00_Boot    │──▶│ 01_Auth     │──▶│ 02_MainMenu    │──▶│ 03_Lobby │──▶│ 04_Game  │
└────────────┘   └─────────────┘   └────────────────┘   └──────────┘   └──────────┘
                                                                            │
                                                                            ▼
                                                                      ┌─────────────┐
                                                                      │ 05_PostMatch│
                                                                      └─────────────┘
```

- **Boot** initializes `NetworkSessionManager`, `GameConfig`, `ProfileStats`.
- **Auth** lets the player register or log in against the backend; the JWT and player name persist to `GameConfig.Preferences` (`PlayerPrefs`).
- **MainMenu** offers Create Sala / Unirse con código / Ajustes / Salir, with a full-screen `LoadingOverlay` while Relay calls run.
- **Lobby** lets the host pick game mode and ability, players toggle Listo, and the host hits Iniciar.
- **Game** is the actual match. HUD elements:
  - Top-left: round timer + live score / DM scoreboard
  - Bottom-left: HP bar (red, fill = current/max) + armor bar (cyan, starts empty, fills 0/50 from pickups)
  - Bottom-right: ammo `N/M`, reload bar, grenade icon (`G` letter, yellow when ready, gray when used), ability icon (`Q` letter colored by ability type)
- **PostMatch** is currently bypassed: the HUD shows a full-screen `BigOverlay` and returns to the main menu after 7 s.

### Controls
| Action     | Bind                        |
|------------|-----------------------------|
| Move       | WASD / Arrows / Left stick  |
| Aim        | Mouse                       |
| Fire       | LMB / RT                    |
| Reload     | R / X                       |
| Grenade    | G / RMB / LT                |
| Ability    | Q / Space / B               |
| Pause      | Esc / Start                 |

---

## 6. Combat tuning

`Assets/ScriptableObjects/Balance/MainBalance.asset`:

| Field                       | Value | Meaning                                                        |
|-----------------------------|-------|----------------------------------------------------------------|
| `playerMaxHealth`           | 100   | Full HP                                                        |
| `moveSpeed`                 | 5     | Units / sec                                                    |
| `grenadesPerRound`          | 1     | Grenade quota at round start                                   |
| `grenadeDamage`             | 40    | Center damage, linear falloff to 0 at radius                   |
| `grenadeRadius`             | 2.5   | Explosion radius                                               |
| `grenadeFuseTime`           | 1.2   | Seconds before detonation                                      |
| `grenadeThrowForce`         | 12    | Initial throw velocity                                         |
| `roundDuration`             | 60    | 1v1 round timer                                                |
| `roundIntroDuration`        | 3     |                                                                |
| `roundEndDuration`          | 4     |                                                                |
| `roundsToWin`               | 5     | First to 5 wins the match (1v1)                                |
| `maxRounds`                 | 9     | Hard cap                                                       |
| `deathmatchDuration`        | 600   | DM timer (10 min)                                              |
| `deathmatchKillsToWin`      | 20    | DM target                                                      |
| `deathmatchRespawnDelay`    | 1.5   | Seconds before respawn                                         |
| Pickup spawn / amounts      | …     | See file                                                       |

`Assets/ScriptableObjects/Weapons/Rifle_Standard.asset`: `damage = 25`, `magazineSize = 8`, `fireRate = 4 shots/s`, `range = 30`. With max HP 100 that is **4 bullets to kill** (no falloff — `WeaponController` calls `DamageDealer.CalculateDamage(weaponData.damage)` with no max range so falloff is disabled).

Abilities (`Assets/ScriptableObjects/Abilities/`):

| Ability  | `value1` | `value2` | `cooldown` | `duration` | Effect                                                       |
|----------|----------|----------|------------|------------|--------------------------------------------------------------|
| Dash     | 4 (dist) | 15 (spd) | 7 s        | 0.3 s      | Owner-side instant teleport, blocks on walls, push-back if clipped |
| Shield   | 60 HP    | 90°      | 12 s       | 2.5 s      | Bubble around player, blocks bullets, decays                 |
| Mine     | 35 dmg   | 1.5 r    | 14 s       | —          | Server-spawned NetworkObject, 0.5 s arm delay, owner-safe    |

---

## 7. Build + distribution

### Building locally
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BuildWindowsRelease.ps1
```

The script (`Tools\BuildWindowsRelease.ps1`) runs Unity in batch mode (`-executeMethod FrentePartido.Editor.StandaloneSmokeBuild.BuildWindowsRelease`) and produces:

- `Builds\Release\FrentePartido\FrentePartido.exe` — the player binary
- `Builds\Release\FrentePartido-Windows.zip` — the same folder zipped for hand-delivery

If the zip step fails with "el archivo está siendo utilizado en otro proceso" the running game holds the data files — close every `FrentePartido.exe` and re-run the script (or `Compress-Archive -Path 'Builds\Release\FrentePartido\*' -DestinationPath 'Builds\Release\FrentePartido-Windows.zip' -Force` directly).

### Distributing to a friend
The `Builds/` folder is **tracked in git** (`.gitignore` excludes `[Bb]uild/` only, not `[Bb]uilds/`). The current Unity build for Windows lives in `Builds/Release/FrentePartido/` plus the matching zip.

- Friend has cloned the repo: `git pull` and the new exe + DLLs land under `Builds/Release/FrentePartido/`. They run `CLIENT_RADMIN.bat` and join.
- Friend has only the zip: extract it on top of the existing folder.

### One-click launch scripts
- `HOST_RADMIN.bat` — starts the auth backend (via `Tools\StartAuthBackendRadmin.ps1`) then launches `Builds\Release\FrentePartido\FrentePartido.exe`.
- `CLIENT_RADMIN.bat` — just launches the same exe (no backend needed).

---

## 8. Important fixes applied during development

This is the chronological list of in-engine bugs that were fixed and are worth knowing about when something drifts again:

1. **HUD never updated**: `HUDController.Initialize()` was never called. Now `HUDController.Update` auto-binds to `NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject()` on the first frame the local player exists, then pushes initial values into the UI manually because `NetworkVariable` initial sync skips `OnValueChanged`.
2. **Round end never fired**: `RoundManager.RegisterPlayers` was never called. `RoundManager.Update` (server) now auto-discovers both `PlayerHealth` instances via `NetworkGameState.Player1ClientId` / `Player2ClientId`.
3. **HP / armor bars didn't visually fill**: the saved scene had `Image.Type = Simple` so `fillAmount` was a no-op. `GameplayVisualNormalizer.SkinHud` now forces `Type = Filled / FillMethod = Horizontal / FillOrigin = Left` and assigns a 4×4 white sprite generated at runtime (`Resources.GetBuiltinResource` is editor-only, returns null in builds).
4. **Client lag**: server-authoritative `NetworkTransform` rubber-banded the client one RTT behind. Replaced with `ClientNetworkTransform` (owner-authoritative) on the Player prefab.
5. **Ability did not fire on the client**: dash needs to run on the owner since the server can no longer write the client's transform. `AbilityController.HandleAbilityInput` runs the dash locally first, then sends the cooldown RPC.
6. **Grenades never exploded on damage**: the LOS check was using `obstacleLayer = ~0` so the linecast hit the player's own collider / triggers. Replaced with `HasClearGrenadeLineOfSight` that only treats `Wall_*`, `Cover_*`, `Decor_Crate*`, `Decor_Barrel*` as obstacles.
7. **Lobby ability text invisible**: the `HorizontalLayoutGroup` was collapsing children. Switched to manual 1/3 anchors via `AbilityBtnAt` and explicitly assigned `LiberationSans SDF` on every Txt.
8. **Mode flip reverted to 1v1**: `SelectMode` rebuilt slots before pushing the new mode to `NetworkSessionManager`, and the periodic refresh inside `UpdatePlayerList` then re-entered `SelectMode(Rounds1v1, false)` immediately. Order swapped — `SetGameMode` now runs first.
9. **Joiner showed up as "Jugador 3"**: the first `SendLocalLobbyInfo` sometimes fires before `IsListening` flips true. Added `RepublishBootstrap` so the lobby state is re-sent at +0.4 s, +1 s, +2 s, +4 s.
10. **DM client final score off by one**: `Player1Kills` / `Player2Kills` `NetworkVariable` updates raced the `NotifyMatchEndClientRpc`. `EndMatch` now caches `FinalP1Score` / `FinalP2Score` and the ClientRpc carries the values explicitly.
11. **DM spawns on top of each other / players stuck**: `GetDeathmatchSpawn` now uses `MapDefinition.deathmatchSpawnPoints` distributed by actual player count, and `PlayerMotor2D` clamps to `RuntimeMatchSettings.BoundsMin/Max` (not the 1v1 box) when in deathmatch.
12. **Dash phased through walls**: `ResolveBlockingDelta` lowered the normal-vs-direction dot threshold from 0.35 to 0.05; added `DepenetrateFromBlockers` which is called at dash end to push the player out of any wall it clipped.

---

## 9. Things still on the radar

- DM spawn count adjusts dynamically; visual spawn markers under `~DeathmatchArena` always show 10 regardless of actual player count.
- Beacon mechanic exists in code (`BeaconCaptureController`) but is currently sidelined since neither mode uses it as a primary objective.
- No replays / spectator mode.
- The match-end overlay reuses the in-game HUD — `05_PostMatch` scene is largely cosmetic right now.

---

## 10. Quick reference

```powershell
# Start backend on the host PC (run once per session)
.\HOST_RADMIN.bat                   # backend + game in one click

# Build a fresh Windows release
.\Tools\BuildWindowsRelease.ps1

# Send to a friend
git push                            # they pull from master
# or hand them Builds\Release\FrentePartido-Windows.zip
```

```bash
# Sanity-check the auth backend from a teammate's machine
curl http://26.17.117.206:3001/health
# {"status":"ok"}
```
