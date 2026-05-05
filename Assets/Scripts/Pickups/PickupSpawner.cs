using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using FrentePartido.Data;

namespace FrentePartido.Pickups
{
    public class PickupSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject _healthPickupPrefab;
        [SerializeField] private GameObject _ammoPickupPrefab;
        [SerializeField] private GameObject _armorPickupPrefab;
        [SerializeField] private MapDefinition _mapDefinition;
        [SerializeField] private BalanceTuningData _balance;

        private readonly List<NetworkObject> _activePickups = new();
        private float _roundElapsed;
        private bool _wave1Spawned;
        private bool _wave2Spawned;

        private readonly Dictionary<NetworkObject, float> _respawnTimers = new();

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            ResetPickups();
        }

        private void Update()
        {
            if (!IsServer) return;

            _roundElapsed += Time.deltaTime;

            if (!_wave1Spawned && _roundElapsed >= _balance.pickup1SpawnTime)
            {
                SpawnWave1();
                _wave1Spawned = true;
            }

            if (!_wave2Spawned && _roundElapsed >= _balance.pickup2SpawnTime)
            {
                SpawnWave2();
                _wave2Spawned = true;
            }

            // Handle respawns
            var toRespawn = new List<NetworkObject>();
            var keys = new List<NetworkObject>(_respawnTimers.Keys);
            foreach (var pickup in keys)
            {
                if (pickup == null) { toRespawn.Add(pickup); continue; }
                _respawnTimers[pickup] -= Time.deltaTime;
                if (_respawnTimers[pickup] <= 0f)
                {
                    var pickupBase = pickup.GetComponent<PickupBase>();
                    if (pickupBase != null) pickupBase.MakeAvailable();
                    toRespawn.Add(pickup);
                }
            }
            foreach (var p in toRespawn) _respawnTimers.Remove(p);
        }

        private void SpawnWave1()
        {
            if (_mapDefinition == null || _mapDefinition.pickupSpawnPoints.Length < 2) return;

            SpawnPickup(_healthPickupPrefab, _mapDefinition.pickupSpawnPoints[0]);
            SpawnPickup(_ammoPickupPrefab, _mapDefinition.pickupSpawnPoints[1]);
        }

        private void SpawnWave2()
        {
            if (_mapDefinition == null || _mapDefinition.pickupSpawnPoints.Length < 3) return;

            SpawnPickup(_armorPickupPrefab, _mapDefinition.pickupSpawnPoints[2]);
        }

        private void SpawnPickup(GameObject prefab, Vector2 position)
        {
            if (prefab == null) return;

            position = ResolveSafePickupPosition(position);
            var go = Instantiate(prefab, position, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                _activePickups.Add(netObj);
            }
        }

        private static Vector2 ResolveSafePickupPosition(Vector2 desired)
        {
            Vector2[] offsets =
            {
                Vector2.zero,
                new Vector2(0f, 0.8f),
                new Vector2(0f, -0.8f),
                new Vector2(0.8f, 0f),
                new Vector2(-0.8f, 0f),
                new Vector2(0.8f, 0.8f),
                new Vector2(-0.8f, 0.8f),
                new Vector2(0.8f, -0.8f),
                new Vector2(-0.8f, -0.8f),
                new Vector2(1.3f, 0f),
                new Vector2(-1.3f, 0f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 candidate = desired + offsets[i];
                if (IsPickupSpotClear(candidate)) return candidate;
            }

            return desired;
        }

        private static bool IsPickupSpotClear(Vector2 position)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, 0.45f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit.isTrigger) continue;
                string n = hit.gameObject.name;
                if (n.StartsWith("Wall_") || n.StartsWith("Cover_") || n.StartsWith("Decor_"))
                    return false;
            }

            return true;
        }

        public void ScheduleRespawn(NetworkObject pickup)
        {
            if (!_respawnTimers.ContainsKey(pickup))
                _respawnTimers[pickup] = _balance.pickupRespawnTime;
        }

        public void ResetPickups()
        {
            if (!IsServer) return;

            foreach (var pickup in _activePickups)
            {
                if (pickup != null && pickup.IsSpawned)
                    pickup.Despawn();
            }
            _activePickups.Clear();
            _respawnTimers.Clear();
            _roundElapsed = 0f;
            _wave1Spawned = false;
            _wave2Spawned = false;
        }
    }
}
