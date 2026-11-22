using System;

namespace Emberfall.Core
{
    public struct DamageRequest
    {
        public float BasePower;      // weapon power after attribute scaling
        public float ComboMultiplier; // 1.0 normal, 1.5 third combo hit, 2.0 heavy
        public float Variance;        // -0.1 .. 0.1, rolled by caller (seeded)
        public bool IsCritical;
        public int TargetArmour;
        public float Resistance;      // 0..0.9 fraction reduced after armour
    }

    public struct DamageResult
    {
        public int Amount;
        public bool WasCritical;
    }

    /// <summary>Deterministic damage pipeline. Pure, unit-tested.</summary>
    public static class CombatMath
    {
        public const float CritMultiplier = 1.5f;

        public static DamageResult Resolve(DamageRequest req)
        {
            float dmg = req.BasePower * req.ComboMultiplier;
            dmg *= 1f + Clamp(req.Variance, -0.1f, 0.1f);
            if (req.IsCritical) dmg *= CritMultiplier;
            dmg *= 100f / (100f + Math.Max(0, req.TargetArmour));
            dmg *= 1f - Clamp(req.Resistance, 0f, 0.9f);
            int amount = (int)Math.Round(dmg, MidpointRounding.AwayFromZero);
            if (amount < 1) amount = 1; // every hit does at least 1
            return new DamageResult { Amount = amount, WasCritical = req.IsCritical };
        }

        /// <summary>Rolls variance in [-0.1, 0.1] from a caller-owned RNG.</summary>
        public static float RollVariance(Random rng)
        {
            return (float)(rng.NextDouble() * 0.2 - 0.1);
        }

        public static bool RollCrit(Random rng, float critChance)
        {
            return rng.NextDouble() < critChance;
        }

        /// <summary>Enemy difficulty scaling by dungeon depth: 1 + 0.12 per floor.</summary>
        public static float DepthScaling(int floorIndex)
        {
            if (floorIndex < 0) floorIndex = 0;
            return 1f + 0.12f * floorIndex;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }

    /// <summary>
    /// Frame-independent combat state: stamina, dodge i-frames, light combo window,
    /// parry window. Advanced by Tick(dt). Pure C#, unit-tested without Unity.
    /// </summary>
    public sealed class CombatState
    {
        public const float DodgeCost = 20f;
        public const float HeavyCost = 35f;
        public const float LightCost = 10f;
        public const float DodgeIFrames = 0.25f;
        public const float ComboWindow = 0.55f;
        public const float ParryWindow = 0.18f;
        public const float HeavyWindup = 0.8f;
        public const int ComboLength = 3;

        public float Stamina { get; private set; }
        public float MaxStamina { get; private set; }
        public float RegenPerSecond { get; private set; }
        public int ComboStep { get; private set; }          // 0 = none, 1..3
        public bool IsInvulnerable { get { return _iFrameTimer > 0f; } }
        public bool IsParrying { get { return _parryTimer > 0f; } }
        public bool IsWindingUp { get { return _windupTimer > 0f; } }
        public bool IsBlocking { get; private set; }

        private float _iFrameTimer;
        private float _comboTimer;
        private float _parryTimer;
        private float _windupTimer;
        private float _regenDelayTimer;

        public CombatState(float maxStamina, float regenPerSecond)
        {
            MaxStamina = maxStamina;
            Stamina = maxStamina;
            RegenPerSecond = regenPerSecond;
        }

        public void Tick(float dt)
        {
            if (dt < 0f) return;
            if (_iFrameTimer > 0f) _iFrameTimer -= dt;
            if (_parryTimer > 0f) _parryTimer -= dt;
            if (_windupTimer > 0f) _windupTimer -= dt;
            if (_regenDelayTimer > 0f) _regenDelayTimer -= dt;
            if (_comboTimer > 0f)
            {
                _comboTimer -= dt;
                if (_comboTimer <= 0f) ComboStep = 0;
            }
            if (_regenDelayTimer <= 0f && !IsBlocking)
            {
                Stamina += RegenPerSecond * dt;
                if (Stamina > MaxStamina) Stamina = MaxStamina;
            }
        }

        /// <summary>Starts a light attack. Returns the combo multiplier (1 or 1.5), or 0 if it cannot start.</summary>
        public float TryLightAttack()
        {
            if (IsWindingUp || Stamina < LightCost) return 0f;
            Stamina -= LightCost;
            _regenDelayTimer = 0.6f;
            ComboStep = ComboStep >= ComboLength ? 1 : ComboStep + 1;
            _comboTimer = ComboWindow;
            return ComboStep == ComboLength ? 1.5f : 1f;
        }

        /// <summary>Starts a heavy attack windup. Returns the multiplier (2) or 0.</summary>
        public float TryHeavyAttack()
        {
            if (IsWindingUp || Stamina < HeavyCost) return 0f;
            Stamina -= HeavyCost;
            _regenDelayTimer = 0.8f;
            _windupTimer = HeavyWindup;
            ComboStep = 0;
            _comboTimer = 0f;
            return 2f;
        }

        public bool TryDodge()
        {
            if (IsInvulnerable || Stamina < DodgeCost) return false;
            Stamina -= DodgeCost;
            _regenDelayTimer = 0.4f;
            _iFrameTimer = DodgeIFrames;
            return true;
        }

        public void SetBlocking(bool blocking)
        {
            IsBlocking = blocking;
        }

        /// <summary>Call when block starts: opens the parry window.</summary>
        public void BeginParryWindow()
        {
            _parryTimer = ParryWindow;
        }

        /// <summary>Interrupts a heavy windup (staggered).</summary>
        public void Stagger()
        {
            _windupTimer = 0f;
            ComboStep = 0;
            _comboTimer = 0f;
        }

        /// <summary>Resolves an incoming hit against dodge/block/parry. Returns damage fraction taken (0..1).</summary>
        public float IncomingHitFraction()
        {
            if (IsInvulnerable) return 0f;
            if (IsParrying) return 0f;
            if (IsBlocking) return 0.3f;
            return 1f;
        }

        public void RestoreStamina(float amount)
        {
            Stamina += amount;
            if (Stamina > MaxStamina) Stamina = MaxStamina;
        }
    }
}
