using UnityEngine;

namespace ARMON.Data
{
    [CreateAssetMenu(menuName = "ARMON/Biome Pool", fileName = "BiomePool_New")]
    public class BiomePool : ScriptableObject
    {
        public string biomeName;          // "forest", "grassland", "rocky", "mixed", "normal"
        public PokemonSpecies common;
        public PokemonSpecies rare;
        public PokemonSpecies legendary;  // null is allowed (normal pool)

        public PokemonSpecies GetByRarity(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common:    return common;
                case Rarity.Rare:      return rare ?? common;
                case Rarity.Legendary: return legendary ?? rare ?? common;
                default: return common;
            }
        }
    }
}
