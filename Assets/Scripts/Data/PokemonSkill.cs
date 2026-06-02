using System;
using UnityEngine;

namespace ARMON.Data
{
    [Serializable]
    public class PokemonSkill
    {
        public string skillName = "Tackle";
        [Tooltip("Multiplied with species baseAttack to compute damage.")]
        public float damageMultiplier = 1.5f;
        public Color beamColor = Color.white;
        public float cooldownSeconds = 1.5f;

        public int DamageAgainst(int attackerBaseAttack)
            => Mathf.Max(1, Mathf.RoundToInt(attackerBaseAttack * damageMultiplier));
    }
}
