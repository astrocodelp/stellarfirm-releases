using System;

namespace Emberfall.Core
{
    /// <summary>
    /// The Ember (light) economy: drains in the Hollow, gained from kills and
    /// shrines, halved on death (recoverable at the death-mark). Low ember
    /// makes the dark hunt you. Pure state, unit-tested.
    /// </summary>
    public sealed class EmberState
    {
        public const float DrainPerSecond = 1f;
        public const float KillGain = 5f;
        public const float ShrineGain = 25f;
        public const float BossGain = 50f;
        public const float LowEmberFraction = 0.25f;
        public const float LowEmberAggressionBonus = 0.2f;

        public float Current { get; private set; }
        public float Capacity { get; private set; }
        public float DeathMarkEmber { get; private set; }
        public bool HasDeathMark { get; private set; }

        public bool IsLow { get { return Capacity > 0f && Current / Capacity < LowEmberFraction; } }
        public float AggressionMultiplier { get { return IsLow ? 1f + LowEmberAggressionBonus : 1f; } }

        public EmberState(float capacity, float current)
        {
            Capacity = capacity < 1f ? 1f : capacity;
            Current = Clamp(current, 0f, Capacity);
        }

        public void SetCapacity(float capacity)
        {
            Capacity = capacity < 1f ? 1f : capacity;
            if (Current > Capacity) Current = Capacity;
        }

        /// <summary>Drains while inside the Hollow. Never below zero.</summary>
        public void TickInHollow(float dt)
        {
            if (dt <= 0f) return;
            Current -= DrainPerSecond * dt;
            if (Current < 0f) Current = 0f;
        }

        /// <summary>Restores a saved value, clamped to [0, capacity].</summary>
        public void SetCurrent(float value)
        {
            Current = Clamp(value, 0f, Capacity);
        }

        public void Gain(float amount)
        {
            if (amount <= 0f) return;
            Current += amount;
            if (Current > Capacity) Current = Capacity;
        }

        /// <summary>Spends ember (healing, crafting, doors). Returns false when unaffordable.</summary>
        public bool TrySpend(float amount)
        {
            if (amount < 0f || amount > Current) return false;
            Current -= amount;
            return true;
        }

        /// <summary>On death: drop half the carried ember at the death-mark. Old mark is lost.</summary>
        public void OnDeath()
        {
            DeathMarkEmber = Current * 0.5f;
            HasDeathMark = DeathMarkEmber > 0f;
            Current -= DeathMarkEmber;
        }

        /// <summary>Touching the death-mark recovers it. Returns the amount recovered.</summary>
        public float RecoverDeathMark()
        {
            if (!HasDeathMark) return 0f;
            float recovered = DeathMarkEmber;
            Gain(recovered);
            DeathMarkEmber = 0f;
            HasDeathMark = false;
            return recovered;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
