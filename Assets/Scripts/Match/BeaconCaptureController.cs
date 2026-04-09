using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Match
{
    public class BeaconCaptureController : NetworkBehaviour
    {
        [SerializeField] private BalanceTuningData _balance;
        [SerializeField] private SpriteRenderer _zoneVisual;
        [SerializeField] private SpriteRenderer _progressVisual;
        [SerializeField] private LayerMask _playerLayer;

        public NetworkVariable<BeaconState> State = new(BeaconState.Inactive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> CaptureProgress = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<ulong> CapturingPlayerId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action<BeaconState> OnBeaconStateChanged;
        public event Action<ulong> OnBeaconCaptured;

        private Dictionary<ulong, float> _presenceTime = new();
        private Networking.NetworkGameState _netState;

        public override void OnNetworkSpawn()
        {
            State.OnValueChanged += (_, newState) =>
            {
                OnBeaconStateChanged?.Invoke(newState);
                UpdateVisuals(newState);
            };

            _netState = Networking.NetworkGameState.Instance;
        }

        private void Update()
        {
            if (!IsServer || State.Value == BeaconState.Inactive || State.Value == BeaconState.Captured) return;

            var playersInZone = DetectPlayersInZone();

            if (playersInZone.Count == 0)
            {
                // Nobody in zone - decay progress
                CaptureProgress.Value = Mathf.Max(0f, CaptureProgress.Value - Time.deltaTime / _balance.beaconCaptureTime);
                if (CaptureProgress.Value <= 0f)
                {
                    State.Value = BeaconState.Active;
                    CapturingPlayerId.Value = 0;
                }
            }
            else if (playersInZone.Count == 1)
            {
                ulong captorId = playersInZone[0];

                // Track presence
                if (!_presenceTime.ContainsKey(captorId)) _presenceTime[captorId] = 0f;
                _presenceTime[captorId] += Time.deltaTime;

                // If different player was capturing, reset
                if (CapturingPlayerId.Value != 0 && CapturingPlayerId.Value != captorId)
                {
                    CaptureProgress.Value = 0f;
                }

                CapturingPlayerId.Value = captorId;

                // Determine capture state
                bool isP1 = _netState != null && captorId == _netState.Player1ClientId.Value;
                State.Value = isP1 ? BeaconState.CapturingP1 : BeaconState.CapturingP2;

                // Progress capture
                CaptureProgress.Value += Time.deltaTime / _balance.beaconCaptureTime;

                if (CaptureProgress.Value >= 1f)
                {
                    CaptureProgress.Value = 1f;
                    State.Value = BeaconState.Captured;
                    OnBeaconCaptured?.Invoke(captorId);

                    var roundMgr = RoundManager.Instance;
                    if (roundMgr != null) roundMgr.HandleBeaconCaptured(captorId);

                    NotifyBeaconCapturedClientRpc(captorId);
                }
            }
            else
            {
                // Both players in zone - contested
                State.Value = BeaconState.Contested;
                foreach (ulong id in playersInZone)
                {
                    if (!_presenceTime.ContainsKey(id)) _presenceTime[id] = 0f;
                    _presenceTime[id] += Time.deltaTime;
                }
            }
        }

        private List<ulong> DetectPlayersInZone()
        {
            var result = new List<ulong>();
            var hits = Physics2D.OverlapCircleAll(transform.position, _balance.beaconRadius, _playerLayer);

            foreach (var hit in hits)
            {
                var netObj = hit.GetComponentInParent<NetworkObject>();
                if (netObj != null)
                {
                    var health = hit.GetComponentInParent<Player.PlayerHealth>();
                    if (health != null && !health.IsDead)
                    {
                        result.Add(netObj.OwnerClientId);
                    }
                }
            }
            return result;
        }

        public void ActivateBeacon()
        {
            if (!IsServer) return;
            State.Value = BeaconState.Active;
            CaptureProgress.Value = 0f;
            CapturingPlayerId.Value = 0;
            NotifyBeaconActivatedClientRpc();
        }

        public void ResetBeacon()
        {
            if (!IsServer) return;
            State.Value = BeaconState.Inactive;
            CaptureProgress.Value = 0f;
            CapturingPlayerId.Value = 0;
            _presenceTime.Clear();
        }

        public float GetPlayerPresenceTime(ulong clientId)
        {
            return _presenceTime.TryGetValue(clientId, out float time) ? time : 0f;
        }

        private void UpdateVisuals(BeaconState state)
        {
            if (_zoneVisual == null) return;

            _zoneVisual.enabled = state != BeaconState.Inactive;

            _zoneVisual.color = state switch
            {
                BeaconState.Active => Color.white,
                BeaconState.Contested => Color.yellow,
                BeaconState.CapturingP1 => new Color(0.3f, 0.5f, 1f),
                BeaconState.CapturingP2 => new Color(1f, 0.3f, 0.3f),
                BeaconState.Captured => Color.green,
                _ => Color.gray
            };
        }

        [ClientRpc]
        private void NotifyBeaconActivatedClientRpc()
        {
            Debug.Log("[Beacon] FARO DE MANDO ACTIVO");
        }

        [ClientRpc]
        private void NotifyBeaconCapturedClientRpc(ulong captorId)
        {
            Debug.Log($"[Beacon] Captured by {captorId} - BOMBARDEO!");
        }
    }
}
