using System;
using System.Collections.Generic;

namespace Emberfall.Core
{
    public enum QuestObjective { Kill, Collect, Talk, Reach }
    public enum QuestStatus { Locked, Active, Completed, Rewarded }

    /// <summary>Immutable quest definition (mirrors ScriptableObject; id-keyed).</summary>
    public sealed class QuestDefinition
    {
        public string Id;
        public string Title;
        public string Description;
        public QuestObjective Objective;
        public string TargetId;
        public int RequiredCount = 1;
        public string[] PrerequisiteQuestIds = Array.Empty<string>();
        public int RewardXp;
        public int RewardEmber;
        public string RewardItemId;      // may be null
        public string RewardWorldFlag;   // may be null
    }

    [Serializable]
    public sealed class QuestState
    {
        public string QuestId;
        public QuestStatus Status;
        public int Counter;
    }

    public struct QuestReward
    {
        public int Xp;
        public int Ember;
        public string ItemId;
        public string WorldFlag;
    }

    /// <summary>
    /// Pure quest state machine. Feed it world events (kills, pickups, talks,
    /// room entries); it advances counters, completes quests and hands out
    /// rewards exactly once. Unit-tested.
    /// </summary>
    public sealed class QuestSystem
    {
        private readonly Dictionary<string, QuestDefinition> _defs = new Dictionary<string, QuestDefinition>();
        private readonly Dictionary<string, QuestState> _states = new Dictionary<string, QuestState>();
        private readonly HashSet<string> _worldFlags = new HashSet<string>();

        public IReadOnlyDictionary<string, QuestState> States { get { return _states; } }
        public IReadOnlyCollection<string> WorldFlags { get { return _worldFlags; } }

        public void Register(QuestDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            _defs[def.Id] = def;
            if (!_states.ContainsKey(def.Id))
                _states[def.Id] = new QuestState { QuestId = def.Id, Status = QuestStatus.Locked };
            RefreshLocks();
        }

        public void LoadState(QuestState state)
        {
            if (state == null) return;
            _states[state.QuestId] = state;
            RefreshLocks();
        }

        public void SetWorldFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _worldFlags.Add(flag);
        }

        /// <summary>World event intake. Returns ids of quests that just completed.</summary>
        public List<string> OnEvent(QuestObjective kind, string targetId, int amount)
        {
            var completed = new List<string>();
            if (amount <= 0) return completed;
            foreach (var kv in _states)
            {
                var state = kv.Value;
                if (state.Status != QuestStatus.Active) continue;
                QuestDefinition def;
                if (!_defs.TryGetValue(state.QuestId, out def)) continue;
                if (def.Objective != kind || def.TargetId != targetId) continue;
                state.Counter += amount;
                if (state.Counter >= def.RequiredCount)
                {
                    state.Counter = def.RequiredCount;
                    state.Status = QuestStatus.Completed;
                    completed.Add(def.Id);
                }
            }
            return completed;
        }

        /// <summary>Claims a completed quest's reward exactly once. Returns false if not claimable.</summary>
        public bool TryClaimReward(string questId, out QuestReward reward)
        {
            reward = default(QuestReward);
            QuestState state;
            if (!_states.TryGetValue(questId, out state)) return false;
            if (state.Status != QuestStatus.Completed) return false;
            QuestDefinition def;
            if (!_defs.TryGetValue(questId, out def)) return false;

            state.Status = QuestStatus.Rewarded;
            reward = new QuestReward
            {
                Xp = def.RewardXp,
                Ember = def.RewardEmber,
                ItemId = def.RewardItemId,
                WorldFlag = def.RewardWorldFlag
            };
            if (!string.IsNullOrEmpty(def.RewardWorldFlag)) _worldFlags.Add(def.RewardWorldFlag);
            RefreshLocks();
            return true;
        }

        /// <summary>Unlocks quests whose prerequisites are all Rewarded.</summary>
        private void RefreshLocks()
        {
            foreach (var kv in _states)
            {
                var state = kv.Value;
                if (state.Status != QuestStatus.Locked) continue;
                QuestDefinition def;
                if (!_defs.TryGetValue(state.QuestId, out def)) continue;
                bool ready = true;
                for (int i = 0; i < def.PrerequisiteQuestIds.Length; i++)
                {
                    QuestState pre;
                    if (!_states.TryGetValue(def.PrerequisiteQuestIds[i], out pre) || pre.Status != QuestStatus.Rewarded)
                    {
                        ready = false;
                        break;
                    }
                }
                if (ready) state.Status = QuestStatus.Active;
            }
        }
    }
}
