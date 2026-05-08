# Marco Polo / Frente Partido

Juego 2D top-down online para PC hecho en Unity. Es un shooter arcade tactico con login, salas online por codigo, lobby, habilidades, pickups, rondas 1v1 y modo deathmatch hasta 10 jugadores.

Estado del proyecto: funcional para pruebas reales con amigos. Build Windows incluida en el repo.

Fecha de este README: 2026-05-08.

---

## Resumen rapido

- Motor: Unity 2022.3.62f1.
- Plataforma principal: Windows PC.
- Networking: host + clientes con Netcode for GameObjects, Unity Relay y Unity Lobby.
- Backend publico: Supabase Edge Function para login, registro y estadisticas.
- Base de datos: Supabase Postgres.
- Build lista: `Builds/Release/FrentePartido/FrentePartido.exe`.
- ZIP para pasar a amigos: `Builds/Release/FrentePartido-Windows.zip`.
- No hace falta Docker ni Radmin para login/stats.
- No hace falta Radmin para jugar si Relay/Lobby de Unity funciona correctamente.
- Docker queda solo como fallback local de desarrollo.

---

## Como jugar con un amigo

### Opcion normal: build ya hecha

1. Descargar o hacer `git pull` del repo.
2. Abrir `Builds/Release/FrentePartido/FrentePartido.exe`.
3. Registrarse o iniciar sesion.
4. Un jugador pulsa `CREAR SALA`.
5. Copia el codigo de sala.
6. El otro jugador pulsa `UNIRSE CON CODIGO` y pega el codigo.
7. En lobby, ambos eligen habilidad/color y pulsan `LISTO`.
8. El host pulsa `INICIAR`.

Los dos ejecutan el mismo `.exe`. No hay exe separado de host/cliente.

### Opcion ZIP

Pasar este archivo:

```text
Builds/Release/FrentePartido-Windows.zip
```

El amigo lo extrae y ejecuta:

```text
FrentePartido.exe
```

---

## URLs y servicios activos

### Backend publico Supabase

El juego apunta por defecto a:

```text
https://kufkgjyeptuzptmegsmf.supabase.co/functions/v1/frentepartido-auth
```

Archivo donde esta configurado:

```text
Assets/Scripts/Core/GameConfig.cs
```

Constante:

```csharp
public const string DEFAULT_AUTH_BASE_URL = "https://kufkgjyeptuzptmegsmf.supabase.co/functions/v1/frentepartido-auth";
```

### Health check

```powershell
curl https://kufkgjyeptuzptmegsmf.supabase.co/functions/v1/frentepartido-auth/health
```

Respuesta esperada:

```json
{"status":"ok","store":"supabase","project":"kufkgjyeptuzptmegsmf"}
```

---

## Funciones implementadas

### Cuenta y perfil

- Pantalla de autenticacion.
- Registro de usuario.
- Login con usuario/email y password.
- Token JWT persistido localmente.
- Auto-login si el token sigue siendo valido.
- Nombre del jugador conectado mostrado en menu/lobby/partida.
- Perfil con estadisticas.
- Estadisticas guardadas en Supabase, no solo local:
  - partidas jugadas
  - victorias
  - derrotas
  - winrate
  - rango
- Registro automatico de resultado al acabar partida.

### Menu principal

- Crear sala.
- Unirse con codigo.
- Perfil.
- Ajustes.
- Salir.
- Overlay de carga para llamadas Relay/Lobby.
- Estetica custom verde/amarillo militar.

### Lobby

- Codigo de sala visible.
- Copiar codigo.
- Lista de jugadores conectados.
- Nombre real de cada jugador.
- Estado conectado/listo.
- Selector de modo de juego.
- Selector de habilidad.
- Selector de color/faccion.
- Boton `LISTO`.
- Boton `INICIAR` solo util para host.
- Validacion de jugadores antes de iniciar.
- Republish de estado de lobby para evitar nombres placeholder tipo `Jugador 2`.

### Modos de juego

#### 1v1 Rondas

- Jugadores: exactamente 2.
- Formato actual: primero a 5 rondas.
- Maximo: 9 rondas.
- Duracion de ronda: 60 segundos.
- Intro de ronda: 3 segundos.
- Fin/intermedio de ronda: 4 segundos.
- Spawn izquierdo/derecho.
- Kill termina la ronda.
- Victoria de partida al llegar a 5 rondas.
- Muerte subita si se acaba el tiempo.
- En muerte subita se rompen coberturas/decorados para forzar pelea.
- Reset de vida, armor, municion, granada, habilidad e input por ronda.
- Fix aplicado: al morir no queda el disparo mantenido para la ronda siguiente.

#### Deathmatch

- Jugadores: de 2 a 10.
- Duracion: 10 minutos.
- Objetivo: primero a 20 kills.
- Si se acaba el tiempo, gana quien tenga mas kills.
- Mapa runtime mas grande que el 1v1.
- Bounds runtime: `-22,-13` a `22,13`.
- El mapa deathmatch limpia paredes/coberturas de la escena 1v1 antes de construir la arena, para que el centro quede realmente abierto y accesible.
- Spawns distribuidos para hasta 10 jugadores.
- Respawn tras morir.
- Delay de respawn: 1.5 segundos.
- Scoreboard en HUD.
- Recompensa de kill: el killer recupera una granada.

---

## Gameplay implementado

### Movimiento

- Movimiento top-down con WASD.
- Apuntado con raton.
- Movimiento sincronizado en red.
- Transform owner-authoritative para reducir input lag del cliente.
- Clamp de posicion por bounds del modo.
- Respawn con snap local para evitar que cliente aparezca en el centro.

### Arma principal

Fusil estandar:

- Damage: 25.
- Cadencia: 4 disparos/segundo.
- Cargador: 8 balas.
- Recarga: 1.4 segundos.
- Rango: 30.
- Spread: 2 grados.
- Damage autoritativo en host.
- Bala/tracer visual mejorado.
- Municion se resetea al inicio de ronda.
- Disparo mantenido se cancela al morir, al desactivar input y al resetear arma.

### Granada

- 1 granada por ronda.
- Tecla: `G`.
- Tambien mouse derecho / left trigger.
- Damage maximo: 40.
- Radio: 2.5.
- Fuse: 1.2 segundos.
- Fuerza de lanzamiento: 12.
- Explosion autoritativa en host.
- Line of sight ajustado para no bloquearse con triggers/cuerpo del jugador.

### Pickups

- Botiquin.
- Municion.
- Armadura.
- Spawns prefijados.
- Primer spawn: segundo 20.
- Segundo spawn: segundo 55.
- Respawn: 15 segundos.
- Botiquin cura 25.
- Si el jugador esta full vida, el sobrante puede convertirse en armadura.
- Armadura suma 30.
- Pickups no deben salir encima de cajas.

### Habilidades

Hay 3 habilidades seleccionables en lobby.

#### Dash / Carrera Tactica

- Tecla: `Q` o `Space`.
- Cooldown: 7 segundos.
- Duracion: 0.3 segundos.
- Distancia: 4.
- Velocidad: 15.
- Se ejecuta en owner para evitar lag.
- Respeta paredes/cajas.
- Depenetracion tras dash para evitar quedarse dentro de colision.

#### Escudo Frontal

- Tecla: `Q` o `Space`.
- Cooldown: 12 segundos.
- Duracion: 2.5 segundos.
- HP del escudo: 60.
- Angulo: 90 grados.
- Absorbe balas antes de quitar vida.
- Visual de escudo alrededor/frente del jugador.

#### Mina de Proximidad

- Tecla: `Q` o `Space`.
- Cooldown: 14 segundos.
- Damage: 35.
- Radio: 1.5.
- Arm delay: 0.5 segundos.
- Mina spawneada como NetworkObject.
- No explota instantaneamente al owner.
- Mina invisible mientras esta colocada.
- Al pisarla, aparece un FX corto de activacion/explosion y texto flotante.
- La victima ve `TE COMISTE UNA MINA`; el resto ve `MINA ACTIVADA`.
- La animacion dura menos de 1 segundo y no bloquea el gameplay.

---

## HUD y UI en partida

- Timer de ronda o deathmatch.
- Marcador 1v1.
- Scoreboard deathmatch.
- Vida abajo izquierda.
- Armadura abajo izquierda.
- Municion abajo derecha.
- Barra de recarga.
- Indicador de granada.
- Indicador de habilidad.
- Cooldown numerico de habilidad.
- Reticula de apuntado visible.
- Overlay grande para intro de ronda, muerte subita y fin de partida.
- Minimapa/visual de arena ajustado en escena.

---

## Arquitectura tecnica

### Unity

Paquetes principales:

- `com.unity.netcode.gameobjects` 1.11.0.
- `com.unity.transport` 2.3.0.
- `com.unity.services.relay` 1.1.1.
- `com.unity.services.lobby` 1.2.2.
- `com.unity.services.authentication` 3.3.3.
- `com.unity.inputsystem` 1.14.0.
- `com.unity.textmeshpro` 3.2.0-pre.12.
- `com.unity.ugui` 2.0.0.

### Networking

- Arquitectura host + clientes.
- Host autoritativo para:
  - damage
  - muertes
  - rondas
  - victoria
  - pickups
  - granadas
  - minas
  - respawns
- Cliente autoritativo para transform propio con `ClientNetworkTransform`.
- Relay para conexion entre PCs.
- Lobby para sala/codigo/metadata.
- La habilidad equipada al empezar partida se toma de la seleccion del lobby; no se permite que una preferencia local vieja sobrescriba la eleccion real.
- Named messages custom para estado de lobby:
  - `FP_LOBBY_UPDATE`
  - `FP_LOBBY_STATE`

### Backend

Produccion:

- Supabase Edge Function Deno.
- Supabase Postgres.
- Tabla `users`.
- Tabla `user_stats`.
- Hash de password con PBKDF2 en Edge Function.
- JWT HMAC SHA-256.

Fallback local:

- Node 20.
- Express.
- Postgres 16 via Docker.
- JSON file fallback si Postgres no esta disponible.

---

## Escenas

Escenas activas en Build Settings:

```text
Assets/Scenes/00_Boot.unity
Assets/Scenes/01_Auth.unity
Assets/Scenes/02_MainMenu.unity
Assets/Scenes/03_Lobby.unity
Assets/Scenes/04_Game.unity
Assets/Scenes/05_PostMatch.unity
```

Flujo:

```text
00_Boot -> 01_Auth -> 02_MainMenu -> 03_Lobby -> 04_Game -> 05_PostMatch / MainMenu
```

Nota: `05_PostMatch` existe, pero gran parte del resultado final se muestra desde overlay HUD dentro de `04_Game`.

---

## Estructura de carpetas

```text
Assets/
  Art/                       Arte 2D y sprites base
  Audio/                     Audio futuro/minimo
  Fonts/                     Fuentes
  Materials/                 Materiales
  Prefabs/                   Prefabs de gameplay/UI/red
  Resources/NetworkPrefabs/  Prefabs cargados/registrados en runtime
  Scenes/                    Escenas principales
  ScriptableObjects/         Balance, armas, habilidades, mapas
  Scripts/
    Abilities/               Dash, shield, mine, controller
    Auth/                    Login/register REST client + UI
    Combat/                  Weapon, projectile, grenade, damage
    Core/                    Bootstrap, config, profile stats, visuals
    Data/                    Enums y ScriptableObjects
    Editor/                  Setup/build/smoke utilities
    Match/                   MatchManager, RoundManager, sudden death, beacon
    Networking/              Relay, Lobby, session, spawn, transforms
    Pickups/                 Health, ammo, armor, spawner
    Player/                  Input, movement, health, aim, presentation
    UI/                      Main menu, lobby, HUD, results
Backend/
  src/server.js              Backend local Node/Express fallback
  supabase/functions/        Edge Function publica
  supabase_schema.sql        Schema Supabase
Tools/
  BuildWindowsRelease.ps1    Build Windows + zip
  ApplySupabaseSchema.ps1    Aplica schema en Supabase por DATABASE_URL
  ConfigureSupabaseBackend.ps1
  StartAuthBackendRadmin.ps1 Fallback local/Radmin
  RunLanHost.ps1             Test LAN local
  RunLanClient.ps1           Test LAN local
Builds/Release/
  FrentePartido/             Build Windows extraida
  FrentePartido-Windows.zip  ZIP para distribuir
```

---

## Archivos importantes

### Gameplay

```text
Assets/Scripts/Combat/WeaponController.cs
Assets/Scripts/Combat/GrenadeController.cs
Assets/Scripts/Player/PlayerHealth.cs
Assets/Scripts/Player/PlayerMotor2D.cs
Assets/Scripts/Abilities/AbilityController.cs
Assets/Scripts/Abilities/DashAbility.cs
Assets/Scripts/Abilities/ShieldAbility.cs
Assets/Scripts/Abilities/MineAbility.cs
Assets/Scripts/Pickups/HealthPickup.cs
Assets/Scripts/Pickups/PickupSpawner.cs
```

### Match y modos

```text
Assets/Scripts/Match/MatchManager.cs
Assets/Scripts/Match/RoundManager.cs
Assets/Scripts/Match/SuddenDeathController.cs
Assets/Scripts/Data/GameEnums.cs
Assets/Scripts/Networking/PlayerSpawnManager.cs
```

### Online

```text
Assets/Scripts/Networking/NetworkSessionManager.cs
Assets/Scripts/Networking/RelayConnectionManager.cs
Assets/Scripts/Networking/LobbyManager.cs
Assets/Scripts/Networking/ClientNetworkTransform.cs
Assets/Scripts/Networking/NetworkPrefabRegistry.cs
```

### UI

```text
Assets/Scripts/UI/MainMenuUI.cs
Assets/Scripts/UI/LobbyUI.cs
Assets/Scripts/UI/HUDController.cs
Assets/Scripts/UI/ResultsUI.cs
Assets/Scripts/Core/GameplayVisualNormalizer.cs
```

### Backend

```text
Assets/Scripts/Auth/AuthService.cs
Assets/Scripts/Auth/AuthUI.cs
Assets/Scripts/Core/ProfileStats.cs
Backend/supabase/functions/frentepartido-auth/index.ts
Backend/supabase_schema.sql
Backend/src/server.js
```

---

## Datos balanceables

### Balance principal

Archivo:

```text
Assets/ScriptableObjects/Balance/MainBalance.asset
```

Valores actuales:

| Campo | Valor |
|---|---:|
| Vida maxima | 100 |
| Velocidad | 5 |
| Granadas por ronda | 1 |
| Damage granada | 40 |
| Radio granada | 2.5 |
| Fuse granada | 1.2 |
| Fuerza granada | 12 |
| Faro activo en | 30s |
| Captura faro | 5s |
| Radio faro | 2 |
| Duracion ronda | 60s |
| Intro ronda | 3s |
| Fin ronda | 4s |
| Muerte subita | 30s |
| Damage muerte subita | 5/s |
| Rondas para ganar | 5 |
| Max rondas | 9 |
| Deathmatch duracion | 600s |
| Deathmatch kills para ganar | 20 |
| Deathmatch respawn | 1.5s |
| Pickup 1 | 20s |
| Pickup 2 | 55s |
| Cura pickup | 25 |
| Armadura pickup | 30 |
| Respawn pickup | 15s |

### Fusil

Archivo:

```text
Assets/ScriptableObjects/Weapons/Rifle_Standard.asset
```

| Campo | Valor |
|---|---:|
| Damage | 25 |
| Fire rate | 4/s |
| Cargador | 8 |
| Recarga | 1.4s |
| Spread | 2 grados |
| Rango | 30 |

### Habilidades

Archivos:

```text
Assets/ScriptableObjects/Abilities/Ability_Dash.asset
Assets/ScriptableObjects/Abilities/Ability_Shield.asset
Assets/ScriptableObjects/Abilities/Ability_Mine.asset
```

| Habilidad | Cooldown | Duracion | Value1 | Value2 |
|---|---:|---:|---:|---:|
| Carrera Tactica | 7s | 0.3s | distancia 4 | velocidad 15 |
| Escudo Frontal | 12s | 2.5s | HP 60 | angulo 90 |
| Mina de Proximidad | 14s | 0s | damage 35 | radio 1.5 |

### Mapa 1v1

Archivo:

```text
Assets/ScriptableObjects/Maps/Map_TrincheraPartida.asset
```

| Campo | Valor |
|---|---|
| Nombre | Trinchera Partida |
| ID | trinchera |
| Spawn A | `(-9, 0)` |
| Spawn B | `(9, 0)` |
| Faro | `(0, 0)` |
| Bounds | `(-10, -6)` a `(10, 6)` |
| Pickups | `(-2.8,-4.4)`, `(2.8,4.4)`, `(0,-4.4)` |

Deathmatch usa arena grande generada en runtime desde `PlayerSpawnManager`.

---

## Controles

| Accion | Tecla / input |
|---|---|
| Mover | WASD / flechas / left stick |
| Apuntar | Raton |
| Disparar | Click izquierdo / right trigger |
| Recargar | R / X gamepad |
| Granada | G / click derecho / left trigger |
| Habilidad | Q / Space / B gamepad |
| Pausa | Esc / Start |

---

## Build Windows

Comando:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BuildWindowsRelease.ps1
```

Salida:

```text
Builds/Release/FrentePartido/FrentePartido.exe
Builds/Release/FrentePartido-Windows.zip
```

El script usa:

```text
C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe
```

Metodo Unity llamado:

```text
FrentePartido.Editor.StandaloneSmokeBuild.BuildWindowsRelease
```

Log:

```text
Temp/unity-release-build.log
```

---

## Scripts de ayuda

### Cliente normal

```text
CLIENT_RADMIN.bat
```

Hace:

```text
start Builds/Release/FrentePartido/FrentePartido.exe
```

### Host antiguo/Radmin/local

```text
HOST_RADMIN.bat
```

Hace:

```text
Tools/StartAuthBackendRadmin.ps1 -Port 3001
start Builds/Release/FrentePartido/FrentePartido.exe
```

Importante: ya no es necesario para Supabase. Solo usar si quieres levantar backend local Docker/Radmin para desarrollo o fallback.

### Aplicar schema Supabase

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\ApplySupabaseSchema.ps1
```

### Configurar backend Supabase local

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\ConfigureSupabaseBackend.ps1
```

---

## API backend

Base URL:

```text
https://kufkgjyeptuzptmegsmf.supabase.co/functions/v1/frentepartido-auth
```

Endpoints:

| Metodo | Ruta | Auth | Uso |
|---|---|---|---|
| GET | `/health` | No | Verificar servicio |
| POST | `/auth/register` | No | Crear cuenta |
| POST | `/auth/login` | No | Iniciar sesion |
| GET | `/auth/verify` | Bearer JWT | Validar token |
| GET | `/profile/stats` | Bearer JWT | Leer stats |
| POST | `/profile/match` | Bearer JWT | Registrar resultado |

Registro:

```json
{
  "username": "peke",
  "email": "peke@example.com",
  "password": "123456",
  "displayName": "Peke"
}
```

Login:

```json
{
  "username": "peke",
  "password": "123456"
}
```

Resultado partida:

```json
{
  "won": true
}
```

---

## Base de datos

Schema:

```text
Backend/supabase_schema.sql
```

Tablas:

### `users`

- `id`
- `username`
- `email`
- `password_hash`
- `display_name`
- `created_at`
- `last_login`

### `user_stats`

- `user_id`
- `matches_played`
- `wins`
- `losses`
- `updated_at`

Rangos calculados por backend:

| Condicion | Rango |
|---|---|
| 0 partidas | SIN RANGO |
| 4+ wins | BRONCE |
| 10+ wins y 50%+ winrate | PLATA |
| 18+ wins y 60%+ winrate | ORO |
| 30+ wins y 70%+ winrate | ELITE |
| otro caso con partidas | RECLUTA |

---

## Testing manual recomendado

### Smoke rapido local

1. Abrir `FrentePartido.exe`.
2. Comprobar login/autologin.
3. Crear sala.
4. Entrar a lobby.
5. Cambiar modo 1v1/deathmatch.
6. Cambiar habilidad.
7. Salir sin errores.

### Test 1v1 real

1. Host crea sala.
2. Cliente entra con codigo.
3. Ambos ven nombres correctos.
4. Ambos pulsan listo.
5. Host inicia.
6. Verificar spawn: host izquierda, cliente derecha.
7. Verificar que nadie se mueve antes del overlay `PELEAD`.
8. Disparar: baja vida del rival.
9. Matar: termina ronda.
10. Nueva ronda: no queda disparo automatico pegado.
11. Municion vuelve a 8/8.
12. Botiquin cura si falta vida.
13. Botiquin a full vida da armadura/sobrante.
14. Granada hace damage.
15. Escudo bloquea balas.
16. Dash no atraviesa cajas.
17. Mina invisible explota al rival y muestra FX/texto al activarse.
18. Al llegar a 5 rondas, acaba partida.
19. Stats suben en perfil.

### Test deathmatch

1. Host crea sala.
2. Cambia modo a `DEATHMATCH`.
3. Entran 2 a 10 jugadores.
4. Todos listos.
5. Host inicia.
6. Verificar mapa grande.
7. Verificar spawns separados.
8. Matar a jugador: suma kill.
9. Victima respawnea.
10. Killer recupera granada.
11. Primer jugador a 20 kills gana.
12. Si pasan 10 minutos, gana top score.

---

## Bugs importantes ya corregidos

- NetworkConfig mismatch por prefabs no registrados.
- Client `NetworkPrefab could not be found`.
- Cliente aparecia en el centro al iniciar ronda.
- Cliente podia rubberbandear por server-authoritative transform.
- Lobby no actualizaba nombres y salia `Jugador 2`.
- Lobby hacia demasiados refresh y daba `Too Many Requests`.
- Cajas atravesables por cliente.
- Balas poco visibles.
- Botiquines encima de cajas.
- Botiquines no curaban por encima de 50.
- Full vida no convertia heal sobrante en armor.
- Escudo visual sin bloquear damage.
- Dash sin desplazamiento real.
- Mina demasiado grande/fea.
- Minas visibles convertidas a minas invisibles con FX solo al activarse.
- Cooldown de habilidades sin feedback correcto.
- HUD de vida/armor mal posicionado.
- Bordes negros molestos en juego.
- Muerte subita dejaba jugador bloqueado.
- Muerte subita ya rompe cobertura.
- Arma desaparecia o no dejaba disparar tras estados raros.
- Disparo quedaba mantenido tras matar y empezaba siguiente ronda disparando.
- Estadisticas locales migradas a Supabase.
- README anterior con datos desfasados/encoding roto.
- Deathmatch parecia abierto pero seguia bloqueado por colliders antiguos del mapa 1v1.
- Seleccionar escudo podia acabar equipando mina por una preferencia local vieja; ahora manda el estado autoritativo del lobby.

---

## Limitaciones conocidas

- El juego todavia no tiene matchmaking publico.
- No hay ranking global visible tipo leaderboard.
- `05_PostMatch` existe, pero el flujo usa overlay de HUD para resultado.
- El faro (`BeaconCaptureController`) existe, pero el modo principal actual prioriza kill/rondas y muerte subita.
- Audio/VFX existen de forma minima; no es una capa final profesional.
- Deathmatch usa mapa generado por runtime, no escena artistica separada final.
- Build Windows es la prioridad; no hay build Android/iOS.

---

## Requisitos para desarrollar

- Unity 2022.3.62f1.
- Windows recomendado.
- Git.
- PowerShell.
- Node/Docker solo si se toca backend local fallback.
- Cuenta/proyecto Unity Services configurado para Lobby/Relay.
- Proyecto Supabase ya creado para backend cloud.

---

## Comandos utiles

```powershell
# Ver estado git
git status --short

# Build Windows
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\BuildWindowsRelease.ps1

# Health Supabase
curl https://kufkgjyeptuzptmegsmf.supabase.co/functions/v1/frentepartido-auth/health

# Lanzar exe
.\Builds\Release\FrentePartido\FrentePartido.exe
```

---

## Notas para futuras mejoras

Prioridad tecnica siguiente:

1. Test real deathmatch con 3+ PCs.
2. Mejorar visual final de mapa deathmatch.
3. Leaderboard global en Supabase.
4. Pantalla `05_PostMatch` completa en vez de overlay.
5. Mas feedback de impacto/audio.
6. Reconexion controlada si cliente cae.
7. Opcion de region/diagnostico de Relay.
8. Capturas oficiales del juego para README.

---

## Capturas

No hay capturas versionadas actualmente en el repo. Cuando se quieran incluir, guardar imagenes en:

```text
Docs/Screenshots/
```

Y enlazarlas asi:

```md
![Menu](Docs/Screenshots/menu.png)
![Lobby](Docs/Screenshots/lobby.png)
![Gameplay](Docs/Screenshots/gameplay.png)
```
