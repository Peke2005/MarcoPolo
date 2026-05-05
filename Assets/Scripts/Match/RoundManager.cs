using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Combat;

namespace FrentePartido.Match
{
    public class RoundManager : NetworkBehaviour
    {
        public static RoundManager Instance { get; private set; }

        [SerializeField] private BalanceTuningData _balance;

        public NetworkVariable<RoundState> State = new(RoundState.Ended, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> RoundTimer = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action<RoundState> OnRoundStateChanged;
        public event Action<ulong> OnRoundWon;
        public event Action OnRoundStarting;

        private BeaconCaptureController _beacon;
        private SuddenDeathController _suddenDeath;
        private Player.PlayerHealth _player1Health;
        private Player.PlayerHealth _player2Health;
        private ulong _player1ClientId;
        private ulong _player2ClientId;
        private bool _roundActive;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            State.OnValueChanged += (_, newState) => OnRoundStateChanged?.Invoke(newState);

            _beacon = FindAnyObjectByType<BeaconCaptureController>();
            _suddenDeath = GetComponent<SuddenDeathController>();
        }

        private void Update()
        {
            // Auto-register players on the server once both have spawned. RegisterPlayers
            // is not called from anywhere else, so without this round end / death
            // detection never fires.
            if (!IsServer) return;
            if (_player1Health != null && _player2Health != null) return;

            var gs = Networking.NetworkGameState.Instance;
            if (gs == null) return;

            ulong p1Id = gs.Player1ClientId.Value;
            ulong p2Id = gs.Player2ClientId.Value;
            if (p1Id == ulong.MaxValue || p2Id == ulong.MaxValue) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;

            Player.PlayerHealth h1 = null;
            Player.PlayerHealth h2 = null;
            foreach (var kv in nm.SpawnManager.SpawnedObjects)
            {
                var obj = kv.Value;
                if (obj == null || !obj.IsPlayerObject) continue;
                var hp = obj.GetComponent<Player.PlayerHealth>();
                if (hp == null) continue;
                if (obj.OwnerClientId == p1Id) h1 = hp;
                else if (obj.OwnerClientId == p2Id) h2 = hp;
            }

            if (h1 != null && h2 != null)
            {
                RegisterPlayers(h1, p1Id, h2, p2Id);
                Debug.Log("[Round] Auto-registered both players.");
            }
        }

        public void RegisterPlayers(Player.PlayerHealth p1, ulong p1Id, Player.PlayerHealth p2, ulong p2Id)
        {
            _player1Health = p1;
            _player1ClientId = p1Id;
            _player2Health = p2;
            _player2ClientId = p2Id;

            _player1Health.OnPlayerDied += HandlePlayerDied;
            _player2Health.OnPlayerDied += HandlePlayerDied;
        }

        public void StartRound()
        {
            if (!IsServer) return;
            StartCoroutine(RoundSequence());
        }

        private IEnumerator RoundSequence()
        {
            // Intro phase
            State.Value = RoundState.Intro;
            RoundTimer.Value = _balance.roundDuration;
            _roundActive = false;

            OnRoundStarting?.Invoke();
            AnnounceRoundClientRpc(MatchManager.Instance.CurrentRound.Value);

            ResetRoundState();

            yield return new WaitForSeconds(_balance.roundIntroDuration);

            // Active phase
            State.Value = RoundState.Active;
            _roundActive = true;
            EnablePlayersClientRpc(true);

            while (_roundActive && RoundTimer.Value > 0f)
            {
                RoundTimer.Value -= Time.deltaTime;

                if (RoundTimer.Value <= (_balance.roundDuration - _balance.beaconActivationTime) &&
                    _beacon != null && _beacon.State.Value == BeaconState.Inactive)
                {
                    _beacon.ActivateBeacon();
                }

                yield return null;
            }

            if (!_roundActive) yield break; // Round ended early by a kill.

            // Time expired without a kill -> sudden death: destroy all cover and let
            // them keep fighting. Round still ends only on a kill.
            yield return StartCoroutine(SuddenDeathSequence());
        }

        private IEnumerator SuddenDeathSequence()
        {
            State.Value = RoundState.SuddenDeath;
            RoundTimer.Value = 0f;

            if (_suddenDeath != null)
                _suddenDeath.StartSuddenDeath();

            AnnounceSuddenDeathClientRpc();

            // No timer: round only ends when a kill fires HandlePlayerDied.
            // Hard cap as safety so a stuck round never hangs forever.
            float guard = 60f;
            while (_roundActive && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }

            if (!_roundActive) yield break;

            // Stuck (no kill in 60s of sudden death) -> killer is whoever has more HP.
            int hp1 = _player1Health != null ? _player1Health.CurrentHealth.Value : 0;
            int hp2 = _player2Health != null ? _player2Health.CurrentHealth.Value : 0;
            ulong winner = hp2 > hp1 ? _player2ClientId : _player1ClientId;
            EndRound(winner);
        }

        private void HandlePlayerDied(ulong killerClientId)
        {
            if (!IsServer || !_roundActive) return;

            // Kill ends the round. Killer wins.
            _roundActive = false;
            EndRound(killerClientId);
        }

        public void HandleBeaconCaptured(ulong captorClientId)
        {
            if (!IsServer || !_roundActive) return;

            _roundActive = false;
            EndRound(captorClientId);
        }

        private void EndRound(ulong winnerClientId)
        {
            _roundActive = false;
            State.Value = RoundState.Ended;

            EnablePlayersClientRpc(false);
            AnnounceRoundWinnerClientRpc(winnerClientId);

            if (_suddenDeath != null)
                _suddenDeath.StopSuddenDeath();

            StartCoroutine(RoundEndDelay(winnerClientId));
        }

        private IEnumerator RoundEndDelay(ulong winnerClientId)
        {
            yield return new WaitForSeconds(_balance.roundEndDuration);
            OnRoundWon?.Invoke(winnerClientId);
        }

        private void ResetRoundState()
        {
            ResetPlayerForRound(_player1Health);
            ResetPlayerForRound(_player2Health);

            // Move them back to spawn points (without this they stay where they died).
            var spawn = FindAnyObjectByType<Networking.PlayerSpawnManager>();
            if (spawn != null) spawn.RespawnPlayers();

            if (_beacon != null) _beacon.ResetBeacon();

            var spawner = FindAnyObjectByType<Pickups.PickupSpawner>();
            if (spawner != null) spawner.ResetPickups();

            ResetPlayerVisualsClientRpc();
            RebuildDecorClientRpc();
        }

        [ClientRpc]
        private void RebuildDecorClientRpc()
        {
            Core.GameplayVisualNormalizer.RebuildDecor();
        }

        private static void ResetPlayerForRound(Player.PlayerHealth health)
        {
            if (health == null) return;

            health.ResetHealth();

            var state = health.GetComponent<Player.PlayerStateController>();
            if (state != null) state.ForceState(PlayerState.Idle);

            var weapon = health.GetComponent<WeaponController>();
            if (weapon != null) weapon.ResetWeapon();
        }

        [ClientRpc]
        private void EnablePlayersClientRpc(bool enabled)
        {
            foreach (var motor in FindObjectsByType<Player.PlayerMotor2D>(FindObjectsSortMode.None))
                motor.SetMovementEnabled(enabled);
        }

        [ClientRpc]
        private void ResetPlayerVisualsClientRpc()
        {
            foreach (var presentation in FindObjectsByType<Player.PlayerPresentation>(FindObjectsSortMode.None))
                presentation.ResetVisuals();
        }

        [ClientRpc]
        private void AnnounceRoundClientRpc(byte roundNumber)
        {
            Debug.Log($"[Round] Round {roundNumber + 1} starting");
        }

        [ClientRpc]
        private void AnnounceRoundWinnerClientRpc(ulong winnerClientId)
        {
            Debug.Log($"[Round] Round won by {winnerClientId}");
        }

        [ClientRpc]
        private void AnnounceSuddenDeathClientRpc()
        {
            Debug.Log("[Round] SUDDEN DEATH");
        }
    }
}
