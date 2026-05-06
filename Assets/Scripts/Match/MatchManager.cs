using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Core;

namespace FrentePartido.Match
{
    public struct DeathmatchScoreData : INetworkSerializable, IEquatable<DeathmatchScoreData>
    {
        public ulong ClientId;
        public FixedString64Bytes PlayerName;
        public byte Kills;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref Kills);
        }

        public bool Equals(DeathmatchScoreData other) => ClientId == other.ClientId;
        public override bool Equals(object obj) => obj is DeathmatchScoreData other && Equals(other);
        public override int GetHashCode() => ClientId.GetHashCode();
    }

    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [SerializeField] private BalanceTuningData _balance;

        public NetworkVariable<byte> Player1Score = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<byte> Player2Score = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<byte> Player1Kills = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<byte> Player2Kills = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<byte> CurrentRound = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<MatchState> State = new(MatchState.WaitingForPlayers, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<GameMode> CurrentGameMode = new(GameMode.Rounds1v1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action<byte, byte> OnScoreChanged;
        public event Action<byte, byte> OnKillsChanged;
        public event Action<ulong> OnMatchWon;
        public event Action OnMatchStarted;
        public event Action OnRematchRequested;
        public event Action OnDeathmatchScoresChanged;

        private RoundManager _roundManager;
        public NetworkList<DeathmatchScoreData> DeathmatchScores { get; private set; }
        public bool IsDeathmatch => CurrentGameMode.Value == GameMode.Deathmatch;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _roundManager = GetComponent<RoundManager>();
            DeathmatchScores = new NetworkList<DeathmatchScoreData>();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Player1Score.OnValueChanged += (_, _) => OnScoreChanged?.Invoke(Player1Score.Value, Player2Score.Value);
            Player2Score.OnValueChanged += (_, _) => OnScoreChanged?.Invoke(Player1Score.Value, Player2Score.Value);
            Player1Kills.OnValueChanged += (_, _) => OnKillsChanged?.Invoke(Player1Kills.Value, Player2Kills.Value);
            Player2Kills.OnValueChanged += (_, _) => OnKillsChanged?.Invoke(Player1Kills.Value, Player2Kills.Value);
            DeathmatchScores.OnListChanged += _ => OnDeathmatchScoresChanged?.Invoke();

            if (IsServer && _roundManager != null)
            {
                _roundManager.OnRoundWon += HandleRoundWon;
            }

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnectedForStart;
                TryAutoStart();
            }
        }

        private void HandleClientConnectedForStart(ulong _) => TryAutoStart();

        private void TryAutoStart()
        {
            if (!IsServer) return;
            if (State.Value == MatchState.InProgress) return;
            if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2) return;

            StartMatch();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _roundManager != null)
            {
                _roundManager.OnRoundWon -= HandleRoundWon;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnectedForStart;
            }
        }

        public void StartMatch()
        {
            if (!IsServer) return;

            Player1Score.Value = 0;
            Player2Score.Value = 0;
            Player1Kills.Value = 0;
            Player2Kills.Value = 0;
            DeathmatchScores.Clear();
            CurrentRound.Value = 0;
            CurrentGameMode.Value = Networking.NetworkSessionManager.Instance != null
                ? Networking.NetworkSessionManager.Instance.SelectedGameMode
                : GameMode.Rounds1v1;
            RuntimeMatchSettings.ApplyMode(CurrentGameMode.Value);

            if (CurrentGameMode.Value == GameMode.Deathmatch)
                InitializeDeathmatchScores();
            State.Value = MatchState.InProgress;

            OnMatchStarted?.Invoke();
            NotifyMatchStartedClientRpc();

            if (IsDeathmatch)
                _roundManager.StartDeathmatch();
            else
                _roundManager.StartRound();
        }

        private void HandleRoundWon(ulong winnerClientId)
        {
            if (!IsServer) return;

            var netState = Networking.NetworkGameState.Instance;
            if (netState == null) return;

            if (winnerClientId == netState.Player1ClientId.Value)
                Player1Score.Value++;
            else if (winnerClientId == netState.Player2ClientId.Value)
                Player2Score.Value++;

            Debug.Log($"[Match] Round won by {winnerClientId}. Score: {Player1Score.Value}-{Player2Score.Value}");

            if (Player1Score.Value >= _balance.roundsToWin)
            {
                EndMatch(netState.Player1ClientId.Value);
            }
            else if (Player2Score.Value >= _balance.roundsToWin)
            {
                EndMatch(netState.Player2ClientId.Value);
            }
            else
            {
                CurrentRound.Value++;
                _roundManager.StartRound();
            }
        }

        private void EndMatch(ulong winnerClientId)
        {
            State.Value = MatchState.PostMatch;
            OnMatchWon?.Invoke(winnerClientId);
            NotifyMatchEndClientRpc(winnerClientId, Player1Score.Value, Player2Score.Value);
            Debug.Log($"[Match] Match won by {winnerClientId}");
        }

        [ClientRpc]
        private void NotifyMatchStartedClientRpc()
        {
            OnMatchStarted?.Invoke();
        }

        [ClientRpc]
        private void NotifyMatchEndClientRpc(ulong winnerClientId, byte p1Score, byte p2Score)
        {
            OnMatchWon?.Invoke(winnerClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRematchServerRpc()
        {
            OnRematchRequested?.Invoke();
            StartMatch();
        }

        /// <summary>
        /// Server-only. Increment the killer's kill count (reuses Player1Score / Player2Score
        /// as the live deathmatch counter shown on the HUD).
        /// </summary>
        public bool RegisterKillServer(ulong killerClientId)
        {
            if (!IsServer) return false;

            var netState = Networking.NetworkGameState.Instance;
            if (netState == null) return false;

            AddDeathmatchKill(killerClientId);
            byte killerKills = GetDeathmatchKills(killerClientId);

            if (killerClientId == netState.Player1ClientId.Value)
                Player1Kills.Value = killerKills;
            else if (killerClientId == netState.Player2ClientId.Value)
                Player2Kills.Value = killerKills;

            int targetKills = _balance != null ? _balance.deathmatchKillsToWin : 20;
            if (killerKills >= targetKills)
            {
                EndMatch(killerClientId);
                return true;
            }

            return false;
        }

        public void FinishDeathmatchByScoreServer()
        {
            if (!IsServer) return;

            var netState = Networking.NetworkGameState.Instance;
            if (netState == null) return;

            ulong winner = ResolveDeathmatchLeader();
            if (winner == ulong.MaxValue)
            {
                winner = Player2Kills.Value > Player1Kills.Value
                    ? netState.Player2ClientId.Value
                    : netState.Player1ClientId.Value;
            }
            EndMatch(winner);
        }

        private void InitializeDeathmatchScores()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            foreach (ulong clientId in nm.ConnectedClientsIds)
            {
                EnsureDeathmatchScore(clientId);
            }
        }

        private void EnsureDeathmatchScore(ulong clientId)
        {
            for (int i = 0; i < DeathmatchScores.Count; i++)
                if (DeathmatchScores[i].ClientId == clientId)
                    return;

            string playerName = $"Jugador {clientId + 1}";
            var session = Networking.NetworkSessionManager.Instance;
            if (session != null)
            {
                foreach (var player in session.GetLobbyPlayers())
                {
                    if (player.ClientId == clientId)
                    {
                        playerName = player.PlayerName;
                        break;
                    }
                }
            }

            DeathmatchScores.Add(new DeathmatchScoreData
            {
                ClientId = clientId,
                PlayerName = new FixedString64Bytes(playerName),
                Kills = 0
            });
        }

        private void AddDeathmatchKill(ulong killerClientId)
        {
            EnsureDeathmatchScore(killerClientId);
            for (int i = 0; i < DeathmatchScores.Count; i++)
            {
                if (DeathmatchScores[i].ClientId != killerClientId) continue;
                var row = DeathmatchScores[i];
                row.Kills = (byte)Mathf.Min(row.Kills + 1, 255);
                DeathmatchScores[i] = row;
                return;
            }
        }

        private byte GetDeathmatchKills(ulong clientId)
        {
            for (int i = 0; i < DeathmatchScores.Count; i++)
                if (DeathmatchScores[i].ClientId == clientId)
                    return DeathmatchScores[i].Kills;
            return 0;
        }

        private ulong ResolveDeathmatchLeader()
        {
            ulong winner = ulong.MaxValue;
            byte best = 0;
            for (int i = 0; i < DeathmatchScores.Count; i++)
            {
                if (winner == ulong.MaxValue || DeathmatchScores[i].Kills > best)
                {
                    winner = DeathmatchScores[i].ClientId;
                    best = DeathmatchScores[i].Kills;
                }
            }
            return winner;
        }
    }
}
