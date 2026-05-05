using System;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Player
{
    /// <summary>
    /// Server-authoritative health and armor system.
    /// Damage is applied on the server; clients are notified via events and ClientRpcs.
    /// </summary>
    public class PlayerHealth : NetworkBehaviour
    {
        [SerializeField] private BalanceTuningData balanceData;

        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> CurrentArmor = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Fires on all clients. Args: (newHealth, maxHealth).</summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>Fires on all clients. Arg: newArmor.</summary>
        public event Action<int> OnArmorChanged;

        /// <summary>Fires on all clients when this player dies. Arg: killer clientId.</summary>
        public event Action<ulong> OnPlayerDied;

        /// <summary>Fires on all clients when damage is received (for visual feedback).</summary>
        public event Action OnDamageReceived;

        private PlayerStateController _stateController;

        public bool IsDead => CurrentHealth.Value <= 0;

        private int MaxHealth => MaxHealthValue;
        public int MaxHealthValue => balanceData != null ? balanceData.playerMaxHealth : 100;

        private void Awake()
        {
            _stateController = GetComponent<PlayerStateController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            CurrentHealth.OnValueChanged += HandleHealthChanged;
            CurrentArmor.OnValueChanged += HandleArmorChanged;

            if (IsServer)
            {
                CurrentHealth.Value = MaxHealth;
                CurrentArmor.Value = 0;
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= HandleHealthChanged;
            CurrentArmor.OnValueChanged -= HandleArmorChanged;
            base.OnNetworkDespawn();
        }

        private void HandleHealthChanged(int oldValue, int newValue)
        {
            OnHealthChanged?.Invoke(newValue, MaxHealth);
        }

        private void HandleArmorChanged(int oldValue, int newValue)
        {
            OnArmorChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Server-only authoritative damage entry point.
        /// Armor absorbs damage first, remainder goes to health.
        /// </summary>
        public void ApplyDamageServer(int damage, ulong sourceClientId)
        {
            if (!IsServer) return;
            if (IsDead) return;
            if (damage <= 0) return;

            int remainingDamage = damage;

            // Armor absorbs first
            if (CurrentArmor.Value > 0)
            {
                int armorAbsorb = Mathf.Min(CurrentArmor.Value, remainingDamage);
                CurrentArmor.Value -= armorAbsorb;
                remainingDamage -= armorAbsorb;
            }

            // Apply remaining to health
            CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - remainingDamage);

            // Notify all clients of damage
            NotifyDamageClientRpc();

            // Check death
            if (CurrentHealth.Value <= 0)
            {
                HandleDeath(sourceClientId);
            }
        }

        /// <summary>
        /// Legacy owner-only RPC kept for local/self effects. Match damage uses ApplyDamageServer.
        /// </summary>
        [ServerRpc]
        public void TakeDamageServerRpc(int damage, ulong sourceClientId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            ApplyDamageServer(damage, sourceClientId);
        }

        /// <summary>
        /// Server-only heal entry point.
        /// </summary>
        public void HealServer(int amount)
        {
            if (!IsServer) return;
            if (IsDead) return;
            if (amount <= 0) return;

            CurrentHealth.Value = Mathf.Min(CurrentHealth.Value + amount, MaxHealth);
        }

        /// <summary>
        /// Legacy owner-only RPC kept for local/self effects. Pickups use HealServer.
        /// </summary>
        [ServerRpc]
        public void HealServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            HealServer(amount);
        }

        /// <summary>
        /// Server-only armor entry point.
        /// </summary>
        public void AddArmorServer(int amount)
        {
            if (!IsServer) return;
            if (IsDead) return;
            if (amount <= 0) return;

            CurrentArmor.Value += amount;
        }

        /// <summary>
        /// Legacy owner-only RPC kept for local/self effects. Pickups use AddArmorServer.
        /// </summary>
        [ServerRpc]
        public void AddArmorServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            AddArmorServer(amount);
        }

        /// <summary>
        /// Reset health and armor for a new round. Server only.
        /// </summary>
        public void ResetHealth()
        {
            if (!IsServer) return;

            CurrentHealth.Value = MaxHealth;
            CurrentArmor.Value = 0;
        }

        private void HandleDeath(ulong killerClientId)
        {
            if (_stateController != null)
                _stateController.SetState(PlayerState.Dead);

            NotifyDeathClientRpc(killerClientId);
        }

        [ClientRpc]
        private void NotifyDamageClientRpc()
        {
            OnDamageReceived?.Invoke();
        }

        [ClientRpc]
        private void NotifyDeathClientRpc(ulong killerClientId)
        {
            OnPlayerDied?.Invoke(killerClientId);
        }
    }
}
