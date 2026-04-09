using System;
using Unity.Netcode;
using FrentePartido.Data;

namespace FrentePartido.Player
{
    /// <summary>
    /// Server-authoritative state machine for player states.
    /// State transitions are validated on the server before being applied.
    /// </summary>
    public class PlayerStateController : NetworkBehaviour
    {
        public NetworkVariable<PlayerState> CurrentState = new NetworkVariable<PlayerState>(
            PlayerState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Fires on all clients when the state changes. Receives the new state.
        /// </summary>
        public event Action<PlayerState> OnStateChanged;

        // Convenience queries
        public bool CanAct => CurrentState.Value != PlayerState.Dead;
        public bool CanShoot => CurrentState.Value == PlayerState.Idle || CurrentState.Value == PlayerState.Moving;
        public bool CanUseAbility => CurrentState.Value == PlayerState.Idle || CurrentState.Value == PlayerState.Moving;
        public bool CanReload => CurrentState.Value == PlayerState.Idle || CurrentState.Value == PlayerState.Moving;
        public bool CanThrowGrenade => CurrentState.Value == PlayerState.Idle || CurrentState.Value == PlayerState.Moving;
        public bool IsDead => CurrentState.Value == PlayerState.Dead;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            CurrentState.OnValueChanged += HandleStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            CurrentState.OnValueChanged -= HandleStateChanged;
            base.OnNetworkDespawn();
        }

        private void HandleStateChanged(PlayerState oldState, PlayerState newState)
        {
            OnStateChanged?.Invoke(newState);
        }

        /// <summary>
        /// Request a state change from the client. Goes through server validation.
        /// </summary>
        [ServerRpc]
        public void RequestStateChangeServerRpc(PlayerState newState, ServerRpcParams rpcParams = default)
        {
            if (ValidateTransition(CurrentState.Value, newState))
            {
                CurrentState.Value = newState;
            }
        }

        /// <summary>
        /// Directly set state on the server (for server-side systems like death, round reset).
        /// </summary>
        public void SetState(PlayerState newState)
        {
            if (!IsServer) return;

            CurrentState.Value = newState;
        }

        /// <summary>
        /// Force state without validation (server only). For round resets.
        /// </summary>
        public void ForceState(PlayerState newState)
        {
            if (!IsServer) return;
            CurrentState.Value = newState;
        }

        /// <summary>
        /// Validates whether a state transition is allowed.
        /// </summary>
        private static bool ValidateTransition(PlayerState from, PlayerState to)
        {
            // Dead is a terminal state - only server ForceState/SetState can leave it
            if (from == PlayerState.Dead)
                return false;

            // Can always transition to Dead
            if (to == PlayerState.Dead)
                return true;

            // Can always go back to Idle or Moving from any living state
            if (to == PlayerState.Idle || to == PlayerState.Moving)
                return true;

            // Action states (Shooting, Reloading, UsingAbility, ThrowingGrenade)
            // can only be entered from Idle or Moving
            switch (to)
            {
                case PlayerState.Shooting:
                case PlayerState.Reloading:
                case PlayerState.UsingAbility:
                case PlayerState.ThrowingGrenade:
                    return from == PlayerState.Idle || from == PlayerState.Moving;

                default:
                    return false;
            }
        }
    }
}
