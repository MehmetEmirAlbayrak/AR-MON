using UnityEngine;

namespace ARMON.Data
{
    public static class EvolutionVisual
    {
        /// <summary>Apply evolved visual to an instantiated Pokémon root (ScaleTint mode only).</summary>
        public static void Apply(GameObject root, PokemonData data, PokemonSpecies species)
        {
            if (root == null || data == null || species == null) return;
            if (!data.hasEvolved) return;
            if (species.evolutionType != EvolutionType.ScaleTint) return;

            root.transform.localScale *= species.evolutionScale;
            foreach (var rend in root.GetComponentsInChildren<Renderer>())
            {
                var mat = rend.material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", species.evolutionTint);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", species.evolutionTint);
            }
        }
    }
}
