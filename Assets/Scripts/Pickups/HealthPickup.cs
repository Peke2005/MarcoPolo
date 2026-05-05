using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Pickups
{
    public class HealthPickup : PickupBase
    {
        [SerializeField] private BalanceTuningData _balance;

        protected override bool ApplyEffect(GameObject player, ulong clientId)
        {
            var health = player.GetComponentInParent<Player.PlayerHealth>();
            if (health == null) return false;
            if (health.CurrentHealth.Value >= health.MaxHealthValue) return false;

            int before = health.CurrentHealth.Value;
            int amount = _balance != null ? _balance.healthPickupAmount : 25;
            health.HealServer(amount);
            Debug.Log($"[Pickup] Health client={clientId} {before}->{health.CurrentHealth.Value}");
            return health.CurrentHealth.Value > before;
        }
    }
}
