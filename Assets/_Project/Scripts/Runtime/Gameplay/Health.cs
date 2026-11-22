using UnityEngine;

namespace Emberfall.Gameplay
{
    /// <summary>
    /// Shared runtime health for player and enemies. Raises plain C# events;
    /// subscribers must unsubscribe in OnDisable/OnDestroy.
    /// </summary>
    public sealed class Health : MonoBehaviour
    {
        public int Max { get; private set; }
        public int Current { get; private set; }
        public bool IsDead { get { return Current <= 0; } }

        public event System.Action<int, int> Changed;   // current, max
        public event System.Action Died;

        public void Init(int max)
        {
            Max = Mathf.Max(1, max);
            Current = Max;
            Changed?.Invoke(Current, Max);
        }

        public void SetMax(int max, bool refill)
        {
            Max = Mathf.Max(1, max);
            if (refill) Current = Max;
            else Current = Mathf.Min(Current, Max);
            Changed?.Invoke(Current, Max);
        }

        /// <summary>Returns damage actually applied.</summary>
        public int ApplyDamage(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            int applied = Mathf.Min(amount, Current);
            Current -= applied;
            Changed?.Invoke(Current, Max);
            if (IsDead) Died?.Invoke();
            return applied;
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke(Current, Max);
        }

        private void OnDestroy()
        {
            Changed = null;
            Died = null;
        }
    }
}
