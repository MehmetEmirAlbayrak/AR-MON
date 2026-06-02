using UnityEngine;

namespace ARMON.Data
{
    public static class RarityRoll
    {
        public const float CommonChance    = 0.72f;
        public const float RareChance      = 0.23f;
        public const float LegendaryChance = 0.05f;

        public static Rarity Roll(float roll01, bool allowLegendary = true)
        {
            roll01 = Mathf.Clamp01(roll01);
            if (allowLegendary && roll01 < LegendaryChance) return Rarity.Legendary;
            if (roll01 < LegendaryChance + RareChance)      return Rarity.Rare;
            return Rarity.Common;
        }
    }
}
