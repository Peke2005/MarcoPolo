using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;
using FrentePartido.Player;

namespace FrentePartido.Match
{
    public class SuddenDeathController : NetworkBehaviour
    {
        [SerializeField] private BalanceTuningData _balance;
        [SerializeField] private float _safeZoneRadius = 3f;
        [SerializeField] private Transform _centerPoint;

        private bool _active;
        private float _damageTickTimer;
        private const float DAMAGE_TICK_INTERVAL = 0.5f;

        public void StartSuddenDeath()
        {
            if (!IsServer) return;
            _active = true;
            _damageTickTimer = 0f;
            ShowSuddenDeathVisualsClientRpc();
        }

        public void StopSuddenDeath()
        {
            _active = false;
            HideSuddenDeathVisualsClientRpc();
        }

        private void Update()
        {
            if (!IsServer || !_active) return;

            _damageTickTimer += Time.deltaTime;
            if (_damageTickTimer < DAMAGE_TICK_INTERVAL) return;
            _damageTickTimer = 0f;

            Vector2 center = _centerPoint != null ? (Vector2)_centerPoint.position : Vector2.zero;
            int tickDamage = Mathf.CeilToInt(_balance.suddenDeathDamagePerSecond * DAMAGE_TICK_INTERVAL);

            foreach (var health in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                if (health.IsDead) continue;

                float dist = Vector2.Distance(health.transform.position, center);
                if (dist > _safeZoneRadius)
                {
                    health.TakeDamageServerRpc(tickDamage, 0);
                }
            }
        }

        [ClientRpc]
        private void ShowSuddenDeathVisualsClientRpc()
        {
            Debug.Log("[SuddenDeath] Zone shrinking - get to center!");
        }

        [ClientRpc]
        private void HideSuddenDeathVisualsClientRpc()
        {
            // Hide danger zone visuals
        }
    }
}
