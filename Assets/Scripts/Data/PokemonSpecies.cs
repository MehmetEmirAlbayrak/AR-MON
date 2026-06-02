using UnityEngine;

namespace ARMON.Data
{
    [CreateAssetMenu(menuName = "ARMON/Pokemon Species", fileName = "Species_New")]
    public class PokemonSpecies : ScriptableObject
    {
        [Header("Identity")]
        public string speciesId;            // stable id, e.g. "forest_common_pidgey"
        public string displayName = "?";
        public BiomeType biomeType;
        public Rarity rarity;

        [Header("Visual")]
        public GameObject basePrefab;
        public Color bodyTint = Color.white;

        [Header("Base Stats (level 1)")]
        public int baseAttack  = 5;
        public int baseHealth  = 20;
        public int baseDefense = 3;
        public int baseSpeed   = 7;

        [Header("Catch & XP")]
        [Range(0.05f, 1f)] public float catchRateBase = 0.8f;
        public float xpYieldMultiplier = 1.0f;

        [Header("Evolution")]
        [Tooltip("-1 means species never evolves")]
        public int evolvesAtLevel = 20;
        public EvolutionType evolutionType = EvolutionType.None;
        public GameObject evolutionPrefab;        // for NewPrefab
        public Color evolutionTint = Color.white; // for ScaleTint
        public float evolutionScale = 1.3f;       // for ScaleTint

        [Header("Skill")]
        public PokemonSkill skill = new PokemonSkill();

        public bool CanEvolveAtLevel(int level)
            => evolutionType != EvolutionType.None
               && evolvesAtLevel > 0
               && level >= evolvesAtLevel;
    }
}
