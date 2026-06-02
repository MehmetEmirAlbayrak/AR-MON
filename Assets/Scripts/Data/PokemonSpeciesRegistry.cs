using System.Collections.Generic;
using UnityEngine;

namespace ARMON.Data
{
    [CreateAssetMenu(menuName = "ARMON/Species Registry", fileName = "SpeciesRegistry")]
    public class PokemonSpeciesRegistry : ScriptableObject
    {
        public List<PokemonSpecies> allSpecies = new List<PokemonSpecies>();
        public List<BiomePool> biomePools = new List<BiomePool>();

        public PokemonSpecies GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var s in allSpecies)
                if (s != null && s.speciesId == id) return s;
            return null;
        }

        public BiomePool GetPoolForBiome(string biome)
        {
            if (string.IsNullOrEmpty(biome)) return GetPoolFallback();
            string norm = biome.ToLowerInvariant().Trim();
            if (norm == "unknown") norm = "normal";
            foreach (var p in biomePools)
                if (p != null && p.biomeName.ToLowerInvariant() == norm)
                    return p;
            return GetPoolFallback();
        }

        BiomePool GetPoolFallback()
        {
            foreach (var p in biomePools)
                if (p != null && p.biomeName.ToLowerInvariant() == "normal") return p;
            return biomePools.Count > 0 ? biomePools[0] : null;
        }
    }
}
