using UnityEngine;

namespace FrentePartido.Pickups
{
    public class AmmoPickup : PickupBase
    {
        protected override void ApplyEffect(GameObject player, ulong clientId)
        {
            var weapon = player.GetComponentInParent<Combat.WeaponController>();
            if (weapon != null)
            {
                weapon.RefillAmmo();
            }
        }
    }
}
