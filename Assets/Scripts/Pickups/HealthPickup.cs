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

            int before = health.CurrentHealth.Value;
            int armorBefore = health.CurrentArmor.Value;
            int amount = _balance != null ? _balance.healthPickupAmount : 25;
            int missingHealth = Mathf.Max(0, health.MaxHealthValue - before);
            int healAmount = Mathf.Min(amount, missingHealth);
            int shieldAmount = amount - healAmount;

            if (healAmount > 0)
                health.HealServer(healAmount);

            // If health is full (or the heal overflows), convert the rest into shield.
            if (shieldAmount > 0)
                health.AddArmorServer(shieldAmount);

            bool changed = health.CurrentHealth.Value > before || health.CurrentArmor.Value > armorBefore;
            Debug.Log($"[Pickup] Health client={clientId} hp {before}->{health.CurrentHealth.Value} armor {armorBefore}->{health.CurrentArmor.Value}");
            return changed;
        }
    }
}
