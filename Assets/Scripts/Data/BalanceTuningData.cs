using UnityEngine;

namespace FrentePartido.Data
{
    [CreateAssetMenu(fileName = "BalanceTuning", menuName = "FrentePartido/Balance Tuning")]
    public class BalanceTuningData : ScriptableObject
    {
        [Header("Player Stats")]
        [Range(50, 200)] public int playerMaxHealth = 100;
        [Range(1f, 15f)] public float moveSpeed = 5f;

        [Header("Grenade")]
        [Range(1, 5)] public int grenadesPerRound = 1;
        [Range(10, 100)] public int grenadeDamage = 40;
        [Range(0.5f, 3f)] public float grenadeRadius = 2.5f;
        [Range(0.5f, 3f)] public float grenadeFuseTime = 1.2f;
        [Range(5f, 30f)] public float grenadeThrowForce = 12f;

        [Header("Beacon (Faro)")]
        [Range(10f, 60f)] public float beaconActivationTime = 30f;
        [Range(2f, 15f)] public float beaconCaptureTime = 5f;
        [Range(1f, 5f)] public float beaconRadius = 2f;

        [Header("Round Timing")]
        [Range(30f, 180f)] public float roundDuration = 90f;
        [Range(1f, 5f)] public float roundIntroDuration = 3f;
        [Range(3f, 10f)] public float roundEndDuration = 6f;
        [Range(5f, 30f)] public float suddenDeathDuration = 15f;
        [Range(1f, 20f)] public float suddenDeathDamagePerSecond = 5f;

        [Header("Match")]
        [Range(1, 5)] public int roundsToWin = 3;
        [Range(3, 9)] public int maxRounds = 5;

        [Header("Pickups")]
        [Range(5f, 40f)] public float pickup1SpawnTime = 20f;
        [Range(30f, 80f)] public float pickup2SpawnTime = 55f;
        [Range(10, 50)] public int healthPickupAmount = 25;
        [Range(10, 50)] public int armorPickupAmount = 30;
        [Range(5f, 30f)] public float pickupRespawnTime = 15f;
    }
}
