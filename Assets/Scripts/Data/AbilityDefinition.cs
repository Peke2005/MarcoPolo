using UnityEngine;

namespace FrentePartido.Data
{
    public enum AbilityType
    {
        Dash,
        Shield,
        Mine
    }

    [CreateAssetMenu(fileName = "NewAbility", menuName = "FrentePartido/Ability Definition")]
    public class AbilityDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string abilityName = "New Ability";
        public string abilityId = "ability_new";
        public AbilityType type;
        public Sprite icon;

        [Header("Timing")]
        [Range(1f, 30f)] public float cooldown = 7f;
        [Range(0f, 10f)] public float duration = 0f;

        [Header("Ability-Specific Values")]
        [Tooltip("Dash: distance | Shield: damage reduction % | Mine: damage")]
        public float value1;
        [Tooltip("Dash: speed | Shield: arc angle | Mine: detection radius")]
        public float value2;

        [Header("Prefab")]
        public GameObject abilityPrefab;

        [Header("Audio")]
        public AudioClip activateSound;
    }
}
