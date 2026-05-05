using UnityEngine;

namespace FrentePartido.Pickups
{
    public class AmmoPickup : PickupBase
    {
        protected override bool ApplyEffect(GameObject player, ulong clientId)
        {
            var weapon = player.GetComponentInParent<Combat.WeaponController>();
            if (weapon == null) return false;
            weapon.RefillAmmo();
            return true;
        }
    }
}
