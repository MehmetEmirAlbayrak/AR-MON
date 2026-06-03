using System;

namespace ARMON.Quest
{
    [Serializable]
    public struct QuestReward
    {
        public RewardItem item;
        public int count;

        public QuestReward(RewardItem item, int count)
        {
            this.item = item;
            this.count = count;
        }
    }
}
