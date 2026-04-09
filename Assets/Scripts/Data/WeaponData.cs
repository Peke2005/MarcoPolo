using UnityEngine;

namespace FrentePartido.Data
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "FrentePartido/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string weaponName = "Fusil Estándar";
        public string weaponId = "rifle_standard";

        [Header("Damage")]
        [Range(1, 100)] public int damage = 20;

        [Header("Fire Rate")]
        [Tooltip("Shots per second")]
        [Range(0.5f, 20f)] public float fireRate = 4f;

        [Header("Magazine")]
        [Range(1, 50)] public int magazineSize = 8;
        [Range(0.5f, 5f)] public float reloadTime = 1.4f;

        [Header("Accuracy")]
        [Range(0f, 15f)] public float spreadAngle = 2f;

        [Header("Range")]
        [Range(1f, 50f)] public float range = 30f;

        [Header("Visuals")]
        public GameObject muzzleFlashPrefab;
        public GameObject impactEffectPrefab;
        public GameObject projectilePrefab;

        [Header("Audio")]
        public AudioClip fireSound;
        public AudioClip reloadSound;
        public AudioClip emptyClickSound;

        public float FireInterval => 1f / fireRate;
    }
}
