using System;
using System.Collections.Generic;
using UnityEngine;
using ARMON.Data;
using ARMON.UI.AR;

namespace ARMON.Quest
{
    /// <summary>
    /// Singleton holding active quests, progress hooks, persistence, and reward grants.
    /// Hook entrypoints:
    ///   - OnPokemonCaught(speciesId)        ← PokeballCollision success path
    ///   - OnWildPokemonDefeated(WildPokemon) ← BattleManager.OnWildPokemonDefeated
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        public const int MaxActiveQuests = 3;
        const string SAVE_KEY = "ARMON_Quests";

        [Tooltip("Species registry — used by QuestRoller to draw trainer teams.")]
        public PokemonSpeciesRegistry registry;

        [Tooltip("Optional reference to BiomePokemonSpawner for current biome.")]
        public BiomePokemonSpawner biomeSpawner;

        [SerializeField] List<QuestInstance> activeQuests = new List<QuestInstance>();

        public IReadOnlyList<QuestInstance> ActiveQuests => activeQuests;

        public event Action OnQuestsChanged;

        // ---------- lifecycle ----------

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- create / cancel ----------

        public bool CanCreateNewQuest() => activeQuests.Count < MaxActiveQuests;

        public string CurrentBiome
        {
            get
            {
                if (biomeSpawner != null && !string.IsNullOrEmpty(biomeSpawner.LastDetectedBiome))
                    return biomeSpawner.LastDetectedBiome;
                return "normal";
            }
        }

        public bool Add(QuestInstance q)
        {
            if (q == null) return false;
            if (!CanCreateNewQuest()) return false;
            activeQuests.Add(q);
            Save();
            OnQuestsChanged?.Invoke();
            return true;
        }

        public void Remove(QuestInstance q)
        {
            if (q == null) return;
            activeQuests.Remove(q);
            Save();
            OnQuestsChanged?.Invoke();
        }

        public QuestInstance FindByAnchorId(string anchorId)
        {
            if (string.IsNullOrEmpty(anchorId)) return null;
            return activeQuests.Find(x => x.npcAnchorId == anchorId);
        }

        /// <summary>Public force-save after a UI mutates a QuestInstance directly.</summary>
        public void OnQuestsChangedForceSave()
        {
            Save();
            OnQuestsChanged?.Invoke();
        }

        // ---------- progress hooks ----------

        public void OnPokemonCaught(string speciesId)
        {
            bool changed = false;
            foreach (var q in activeQuests)
            {
                if (q.state != QuestState.Active) continue;
                if (q.type != QuestType.CatchTarget) continue;
                q.currentCount++;
                if (q.currentCount >= q.targetCount)
                {
                    q.state = QuestState.ObjectiveMet;
                    ToastManager.Show($"Görev tamam: {q.targetCount} Pokemon yakalandı");
                }
                changed = true;
            }
            if (changed) { Save(); OnQuestsChanged?.Invoke(); }
        }

        public void OnWildPokemonDefeated(WildPokemon w)
        {
            if (w == null) return;
            bool changed = false;
            foreach (var q in activeQuests)
            {
                if (q.state != QuestState.Active) continue;

                if (w.isTrainerPokemon)
                {
                    if (q.type != QuestType.TrainerBattle) continue;
                    if (q.npcAnchorId != w.questOwnerAnchorId) continue;
                    q.trainerPokemonDefeated++;
                    if (q.trainerPokemonDefeated >= 3)
                    {
                        q.state = QuestState.ObjectiveMet;
                        ToastManager.Show("Trainer yenildi! Ödülünü çantadan al.");
                    }
                    else
                    {
                        SpawnNextTrainerPokemon(q);
                    }
                    changed = true;
                }
                else
                {
                    if (q.type != QuestType.DefeatWild) continue;
                    q.currentCount++;
                    if (q.currentCount >= q.targetCount)
                    {
                        q.state = QuestState.ObjectiveMet;
                        ToastManager.Show($"Görev tamam: {q.targetCount} vahşi yenildi");
                    }
                    changed = true;
                }
            }
            if (changed) { Save(); OnQuestsChanged?.Invoke(); }
        }

        void SpawnNextTrainerPokemon(QuestInstance q)
        {
            int idx = q.trainerPokemonDefeated; // already incremented → next index
            if (idx >= q.trainerTeamSpeciesIds.Count || registry == null) return;
            string sid = q.trainerTeamSpeciesIds[idx];
            var species = registry.GetById(sid);
            if (species == null || species.basePrefab == null) return;

            QuestGiverNPC owner = null;
            var allNpcs = GameObject.FindObjectsByType<QuestGiverNPC>(FindObjectsSortMode.None);
            foreach (var n in allNpcs)
                if (n.Quest != null && n.Quest.id == q.id) { owner = n; break; }
            if (owner == null)
            {
                Debug.LogWarning("[QuestManager] Trainer NPC not in scene — cannot spawn next pokemon");
                return;
            }
            Vector3 fwd = owner.transform.forward;
            Vector3 spawnPos = owner.transform.position + fwd * 1.5f;
            GameObject mon = GameObject.Instantiate(species.basePrefab, spawnPos, Quaternion.LookRotation(-fwd));
            var wp = mon.GetComponent<WildPokemon>();
            if (wp == null) wp = mon.AddComponent<WildPokemon>();
            wp.species = species;
            wp.pokemonName = species.displayName;
            wp.isTrainerPokemon = true;
            wp.questOwnerAnchorId = q.npcAnchorId;
        }

        // ---------- claim ----------

        public bool TryClaim(QuestInstance q)
        {
            if (q == null) return false;
            if (q.state != QuestState.ObjectiveMet) return false;
            if (PokemonBag.Instance == null)
            {
                Debug.LogWarning("[QuestManager] PokemonBag.Instance null — skipping reward grant.");
                return false;
            }
            foreach (var r in q.rewards) GrantReward(r);
            activeQuests.Remove(q);
            Save();
            OnQuestsChanged?.Invoke();
            return true;
        }

        static void GrantReward(QuestReward r)
        {
            switch (r.item)
            {
                case RewardItem.Pokeball:    PokemonBag.Instance.AddPokeball(r.count); break;
                case RewardItem.SmallPotion: PokemonBag.Instance.AddPotion(PotionType.SmallPotion, r.count); break;
                case RewardItem.SuperPotion: PokemonBag.Instance.AddPotion(PotionType.SuperPotion, r.count); break;
                case RewardItem.HyperPotion: PokemonBag.Instance.AddPotion(PotionType.HyperPotion, r.count); break;
                case RewardItem.Revive:      PokemonBag.Instance.AddPotion(PotionType.Revive, r.count); break;
            }
        }

        // ---------- persistence ----------

        [Serializable]
        class QuestSaveWrapper
        {
            public List<QuestInstance> quests = new List<QuestInstance>();
        }

        void Save()
        {
            var w = new QuestSaveWrapper { quests = activeQuests };
            PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(w));
            PlayerPrefs.Save();
        }

        void Load()
        {
            var s = PlayerPrefs.GetString(SAVE_KEY, "");
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                var w = JsonUtility.FromJson<QuestSaveWrapper>(s);
                activeQuests = w?.quests ?? new List<QuestInstance>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QuestManager] Load failed: {e.Message}");
                activeQuests = new List<QuestInstance>();
            }
        }
    }
}
