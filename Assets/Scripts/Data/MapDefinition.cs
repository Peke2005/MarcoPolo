using UnityEngine;

namespace FrentePartido.Data
{
    [CreateAssetMenu(fileName = "NewMap", menuName = "FrentePartido/Map Definition")]
    public class MapDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string mapName = "New Map";
        public string mapId = "map_new";

        [Header("Spawn Points")]
        public Vector2 spawnPointA = new Vector2(-8f, 0f);
        public Vector2 spawnPointB = new Vector2(8f, 0f);

        [Header("Beacon")]
        public Vector2 beaconPosition = Vector2.zero;

        [Header("Pickup Points")]
        public Vector2[] pickupSpawnPoints = new Vector2[]
        {
            new Vector2(-4f, 3f),
            new Vector2(4f, -3f),
            new Vector2(0f, 4f)
        };

        [Header("Map Bounds")]
        public Vector2 boundsMin = new Vector2(-12f, -7f);
        public Vector2 boundsMax = new Vector2(12f, 7f);
    }
}
