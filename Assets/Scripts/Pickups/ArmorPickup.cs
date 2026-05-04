using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Pickups
{
    public class ArmorPickup : PickupBase
    {
        [SerializeField] private BalanceTuningData _balance;

        protected override void ApplyEffect(GameObject player, ulong clientId)
        {
            var health = player.GetComponentInParent<Player.PlayerHealth>();
            if (health != null)
            {
                int amount = _balance != null ? _balance.armorPickupAmount : 25;
                health.AddArmorServer(amount);
            }
        }
    }
}
