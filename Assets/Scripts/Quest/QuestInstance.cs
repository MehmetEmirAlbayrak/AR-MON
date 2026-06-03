using System;
using System.Collections.Generic;

namespace ARMON.Quest
{
    /// <summary>
    /// Serializable runtime quest record. Persisted via JsonUtility under PlayerPrefs key "ARMON_Quests".
    /// All fields public — required by Unity JsonUtility. Enums serialize as ints.
    /// </summary>
    [Serializable]
    public class QuestInstance
    {
        public string id;
        public QuestType type;
        public int targetCount;
        public int currentCount;
        public QuestState state;
        public List<QuestReward> rewards = new List<QuestReward>();

        public string npcAnchorId;

        public List<string> trainerTeamSpeciesIds = new List<string>();
        public int trainerPokemonDefeated;
    }
}
