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

        [Header("Deathmatch Spawn Points")]
        [Tooltip("Up to 10 spawn anchors used for deathmatch. The active set scales with player count so 2 players spawn far apart, 10 players use the full ring.")]
        public Vector2[] deathmatchSpawnPoints = new Vector2[]
        {
            new Vector2(-18f,   8f),
            new Vector2( 18f,  -8f),
            new Vector2(-18f,  -8f),
            new Vector2( 18f,   8f),
            new Vector2(  0f,  10f),
            new Vector2(  0f, -10f),
            new Vector2(-12f,   0f),
            new Vector2( 12f,   0f),
            new Vector2(-18f,   0f),
            new Vector2( 18f,   0f),
        };

        [Header("Deathmatch Bounds")]
        public Vector2 deathmatchBoundsMin = new Vector2(-21f, -12.5f);
        public Vector2 deathmatchBoundsMax = new Vector2( 21f,  12.5f);

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
