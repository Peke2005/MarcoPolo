using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

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

            if (!_roundActive) yield break; // Round ended by kill or capture

            // Time's up - evaluate winner
            ulong winner = WinConditionEvaluator.EvaluateRoundWinner(
                _player1Health, _player2Health, _beacon,
                _player1ClientId, _player2ClientId);

            if (winner == 0)
            {
                // Sudden death
                yield return StartCoroutine(SuddenDeathSequence());
            }
            else
            {
                EndRound(winner);
            }
        }

        private IEnumerator SuddenDeathSequence()
        {
            State.Value = RoundState.SuddenDeath;
            float timer = _balance.suddenDeathDuration;

            if (_suddenDeath != null)
                _suddenDeath.StartSuddenDeath();

            AnnounceSuddenDeathClientRpc();

            while (timer > 0f && _roundActive)
            {
                timer -= Time.deltaTime;
                RoundTimer.Value = timer;
                yield return null;
            }

            if (!_roundActive) yield break;

            ulong winner = WinConditionEvaluator.EvaluateSuddenDeathWinner(
                _player1Health, _player2Health, _player1ClientId, _player2ClientId);

            if (winner == 0) winner = _player1ClientId; // Fallback: no draws

            EndRound(winner);
        }

        private void HandlePlayerDied(ulong killerClientId)
        {
            if (!IsServer || !_roundActive) return;

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
            if (_player1Health != null) _player1Health.ResetHealth();
            if (_player2Health != null) _player2Health.ResetHealth();

            if (_beacon != null) _beacon.ResetBeacon();

            var spawner = FindAnyObjectByType<Pickups.PickupSpawner>();
            if (spawner != null) spawner.ResetPickups();
        }

        [ClientRpc]
        private void EnablePlayersClientRpc(bool enabled)
        {
            foreach (var motor in FindObjectsByType<Player.PlayerMotor2D>(FindObjectsSortMode.None))
                motor.SetMovementEnabled(enabled);
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
