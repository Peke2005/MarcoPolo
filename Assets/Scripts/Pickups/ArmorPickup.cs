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
                health.AddArmorServerRpc(_balance.armorPickupAmount);
            }
        }
    }
}
