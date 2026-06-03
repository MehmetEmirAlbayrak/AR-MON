using System.Collections.Generic;
using UnityEngine;
using ARMON.Data;

namespace ARMON.Quest
{
    /// <summary>
    /// Random quest generator. Given a biome + species registry, produces a QuestInstance
    /// with type, target count, rewards, and (for TrainerBattle) a 3-species team drawn
    /// from the biome pool.
    /// </summary>
    public static class QuestRoller
    {
        public static QuestInstance Roll(string biome, PokemonSpeciesRegistry registry)
        {
            var q = new QuestInstance
            {
                id = System.Guid.NewGuid().ToString(),
                state = QuestState.Offered,
                currentCount = 0,
                trainerPokemonDefeated = 0
            };

            float r = Random.value;
            if (r < 0.45f) q.type = QuestType.CatchTarget;
            else if (r < 0.90f) q.type = QuestType.DefeatWild;
            else q.type = QuestType.TrainerBattle;

            if (q.type == QuestType.TrainerBattle)
            {
                q.targetCount = 3;
                q.trainerTeamSpeciesIds = RollTrainerTeam(biome, registry);
                q.rewards = new List<QuestReward>
                {
                    new QuestReward(RewardItem.HyperPotion, 1),
                    new QuestReward(RewardItem.Revive,     2),
                    new QuestReward(RewardItem.Pokeball,   3),
                };
            }
            else
            {
                q.targetCount = Random.Range(3, 6);
                q.rewards = RollRewards(q.type, q.targetCount);
            }

            return q;
        }

        static List<QuestReward> RollRewards(QuestType type, int target)
        {
            var list = new List<QuestReward>();
            if (target <= 3)
            {
                list.Add(new QuestReward(RewardItem.Pokeball, 1));
            }
            else if (target == 4)
            {
                list.Add(new QuestReward(RewardItem.Pokeball, 2));
                list.Add(new QuestReward(RewardItem.SmallPotion, 1));
            }
            else
            {
                if (type == QuestType.DefeatWild)
                {
                    list.Add(new QuestReward(RewardItem.Pokeball, 3));
                    list.Add(new QuestReward(RewardItem.SuperPotion, 1));
                }
                else
                {
                    list.Add(new QuestReward(RewardItem.Pokeball, 2));
                    list.Add(new QuestReward(RewardItem.SmallPotion, 1));
                }
            }
            return list;
        }

        static List<string> RollTrainerTeam(string biome, PokemonSpeciesRegistry registry)
        {
            var team = new List<string>(3);
            if (registry == null) return team;

            var pool = registry.GetPoolForBiome(biome);
            var candidates = new List<PokemonSpecies>();
            if (pool != null)
            {
                if (pool.common    != null) candidates.Add(pool.common);
                if (pool.rare      != null) candidates.Add(pool.rare);
                if (pool.legendary != null) candidates.Add(pool.legendary);
            }
            if (candidates.Count == 0)
            {
                foreach (var s in registry.allSpecies)
                    if (s != null) candidates.Add(s);
            }
            if (candidates.Count == 0) return team;

            for (int i = 0; i < 3; i++)
            {
                var s = candidates[Random.Range(0, candidates.Count)];
                team.Add(s.speciesId);
            }
            return team;
        }
    }
}
