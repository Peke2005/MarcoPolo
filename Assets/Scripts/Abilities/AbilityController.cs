using System;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Player;

namespace FrentePartido.Abilities
{
    /// <summary>
    /// Server-authoritative ability system. Manages equipped ability, cooldown,
    /// and delegates execution to type-specific ability scripts.
    /// Attach to player prefab alongside PlayerMotor2D, PlayerHealth, etc.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor2D))]
    [RequireComponent(typeof(PlayerStateController))]
    public class AbilityController : NetworkBehaviour
    {
        [Header("Ability Loadout")]
        [SerializeField] private AbilityDefinition[] availableAbilities = new AbilityDefinition[3];

        [Header("Ability Executors")]
        [SerializeField] private DashAbility dashAbility;
        [SerializeField] private ShieldAbility shieldAbility;
        [SerializeField] private MineAbility mineAbility;

        // ── Network State ───────────────────────────────────────────
        public NetworkVariable<int> EquippedAbilityIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> CooldownRemaining = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ── Events ──────────────────────────────────────────────────
        /// <summary>Args: (remaining, totalCooldown).</summary>
        public event Action<float, float> OnCooldownChanged;

        public event Action OnAbilityUsed;

        // ── Cached Components ───────────────────────────────────────
        private PlayerMotor2D _motor;
        private PlayerInputReader _input;
        private PlayerStateController _stateController;
        private PlayerAimController _aimController;

        private bool _isOnCooldown;

        // ── Properties ──────────────────────────────────────────────
        public AbilityDefinition CurrentAbility =>
            availableAbilities != null &&
            EquippedAbilityIndex.Value >= 0 &&
            EquippedAbilityIndex.Value < availableAbilities.Length
                ? availableAbilities[EquippedAbilityIndex.Value]
                : null;

        // ── Lifecycle ───────────────────────────────────────────────
        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _stateController = GetComponent<PlayerStateController>();
            _input = GetComponent<PlayerInputReader>();
            _aimController = GetComponent<PlayerAimController>();

            if (dashAbility == null) dashAbility = GetComponent<DashAbility>();
            if (shieldAbility == null) shieldAbility = GetComponent<ShieldAbility>();
            if (mineAbility == null) mineAbility = GetComponent<MineAbility>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            CooldownRemaining.OnValueChanged += HandleCooldownChanged;

            if (IsOwner && _input != null)
            {
                _input.OnAbilityPressed += HandleAbilityInput;
            }
        }

        public override void OnNetworkDespawn()
        {
            CooldownRemaining.OnValueChanged -= HandleCooldownChanged;

            if (IsOwner && _input != null)
            {
                _input.OnAbilityPressed -= HandleAbilityInput;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (!_isOnCooldown) return;

            CooldownRemaining.Value -= Time.deltaTime;

            if (CooldownRemaining.Value <= 0f)
            {
                CooldownRemaining.Value = 0f;
                _isOnCooldown = false;
            }
        }

        // ── Owner Input ─────────────────────────────────────────────
        private void HandleAbilityInput()
        {
            if (!IsOwner || !IsSpawned) return;
            if (_stateController != null && !_stateController.CanUseAbility) return;
            if (CooldownRemaining.Value > 0f) return;

            Vector2 aimDir = GetAimDirection();
            UseAbilityServerRpc(aimDir);
        }

        private Vector2 GetAimDirection()
        {
            if (_input != null)
            {
                Vector2 dir = _input.AimWorldPosition - (Vector2)transform.position;
                if (dir.sqrMagnitude > 0.01f) return dir.normalized;

                if (_input.MoveInput.sqrMagnitude > 0.01f)
                    return _input.MoveInput.normalized;
            }

            return Vector2.right;
        }

        // ── Server Logic ────────────────────────────────────────────
        [ServerRpc]
        private void UseAbilityServerRpc(Vector2 aimDirection, ServerRpcParams rpcParams = default)
        {
            if (_isOnCooldown || CooldownRemaining.Value > 0f)
            {
                Debug.Log($"[AbilityController] Ability on cooldown. Remaining: {CooldownRemaining.Value:F1}s");
                return;
            }

            AbilityDefinition ability = CurrentAbility;
            if (ability == null)
            {
                Debug.LogWarning("[AbilityController] No ability equipped or index out of range.");
                return;
            }

            if (_stateController != null && !_stateController.CanUseAbility)
            {
                Debug.Log("[AbilityController] Player state does not allow ability use.");
                return;
            }

            // Set player state to UsingAbility
            if (_stateController != null)
                _stateController.SetState(PlayerState.UsingAbility);

            bool executed = ExecuteAbility(ability, aimDirection);

            if (executed)
            {
                // Start cooldown
                CooldownRemaining.Value = ability.cooldown;
                _isOnCooldown = true;

                NotifyAbilityUsedClientRpc();
                Debug.Log($"[AbilityController] {ability.abilityName} executed. Cooldown: {ability.cooldown}s");
            }

            // Return to Idle state after ability
            if (_stateController != null)
                _stateController.SetState(PlayerState.Idle);
        }

        private bool ExecuteAbility(AbilityDefinition ability, Vector2 aimDirection)
        {
            switch (ability.type)
            {
                case AbilityType.Dash:
                    if (dashAbility != null)
                    {
                        dashAbility.Execute(_motor, aimDirection, ability.value1, ability.value2);
                        return true;
                    }
                    Debug.LogWarning("[AbilityController] DashAbility component missing.");
                    return false;

                case AbilityType.Shield:
                    if (shieldAbility != null)
                    {
                        shieldAbility.ActivateServerRpc(aimDirection, ability.duration, ability.value1);
                        return true;
                    }
                    Debug.LogWarning("[AbilityController] ShieldAbility component missing.");
                    return false;

                case AbilityType.Mine:
                    if (mineAbility != null)
                    {
                        Vector2 placePos = (Vector2)transform.position + aimDirection.normalized * 1.5f;
                        mineAbility.PlaceMineServerRpc(placePos, (int)ability.value1, ability.value2);
                        return true;
                    }
                    Debug.LogWarning("[AbilityController] MineAbility component missing.");
                    return false;

                default:
                    Debug.LogWarning($"[AbilityController] Unknown ability type: {ability.type}");
                    return false;
            }
        }

        // ── Client Notifications ────────────────────────────────────
        [ClientRpc]
        private void NotifyAbilityUsedClientRpc()
        {
            OnAbilityUsed?.Invoke();
        }

        private void HandleCooldownChanged(float oldValue, float newValue)
        {
            AbilityDefinition ability = CurrentAbility;
            float total = ability != null ? ability.cooldown : 1f;
            OnCooldownChanged?.Invoke(newValue, total);
        }

        // ── Public API ──────────────────────────────────────────────

        /// <summary>
        /// Set equipped ability index. Called from lobby selection.
        /// </summary>
        public void SetAbility(int index)
        {
            if (index < 0 || index >= availableAbilities.Length)
            {
                Debug.LogWarning($"[AbilityController] Invalid ability index: {index}");
                return;
            }

            if (IsServer)
            {
                EquippedAbilityIndex.Value = index;
            }
            else
            {
                SetAbilityServerRpc(index);
            }
        }

        [ServerRpc]
        private void SetAbilityServerRpc(int index, ServerRpcParams rpcParams = default)
        {
            if (index >= 0 && index < availableAbilities.Length)
            {
                EquippedAbilityIndex.Value = index;
                Debug.Log($"[AbilityController] Player {OwnerClientId} equipped ability index {index}");
            }
        }

        /// <summary>
        /// Reset cooldown for new round.
        /// </summary>
        public void ResetCooldown()
        {
            if (!IsServer) return;

            CooldownRemaining.Value = 0f;
            _isOnCooldown = false;
        }
    }
}
