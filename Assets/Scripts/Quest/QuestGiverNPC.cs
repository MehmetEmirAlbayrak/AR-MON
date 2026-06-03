using UnityEngine;

namespace ARMON.Quest
{
    /// <summary>Stub — real component completes in next step.</summary>
    public class QuestGiverNPC : MonoBehaviour
    {
        public QuestInstance Quest { get; protected set; }
        public virtual void Bind(QuestInstance q) { Quest = q; }
    }
}
