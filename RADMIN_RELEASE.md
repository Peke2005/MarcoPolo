# Frente Partido - Radmin release

## Auth backend target

Primary auth URL baked into build:

- `http://26.17.117.206:3001`

Fallback auth URL:

- `http://26.234.30.190:3001`

Clients must be connected to the same Radmin VPN if auth is hosted on a `26.x.x.x` private Radmin IP.

## Backend host PC

Run from repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\StartAuthBackendRadmin.ps1 -RadminIp 26.17.117.206 -Port 3001
```

Expected health:

```json
{"status":"ok"}
```

If `26.17.117.206:3001` times out:

- Radmin VPN not connected, or wrong Radmin IP.
- Docker backend not running.
- Windows Firewall blocking TCP `3001`.
- Run script as admin once to create firewall rule.

## Player build

Build + zip:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BuildWindowsRelease.ps1
```

Output to send friends:

- `Builds\Release\FrentePartido-Windows.zip`

## Friend test flow

1. Friend joins same Radmin VPN.
2. Friend extracts `FrentePartido-Windows.zip`.
3. Friend runs `FrentePartido.exe`.
4. Login/register.
5. Host creates room, copies Relay code.
6. Friend joins with Relay code.
7. Both press `LISTO`; host presses `INICIAR`.

## APK note

Literal Android `.apk` is not prepared in this project: current game is PC controls (`WASD`, mouse, `R`, `G`, `Shift/Space`) and this Unity install has no Android Build Support. Current shippable build is Windows x64 ZIP.
