using System;
using Emberfall.Core;
using NUnit.Framework;

namespace Emberfall.Tests.EditMode
{
    public sealed class CombatMathTests
    {
        [Test]
        public void Resolve_ZeroArmour_NoVariance_ReturnsBase()
        {
            var r = CombatMath.Resolve(new DamageRequest { BasePower = 20f, ComboMultiplier = 1f });
            Assert.AreEqual(20, r.Amount);
        }

        [Test]
        public void Resolve_Armour100_HalvesDamage()
        {
            var r = CombatMath.Resolve(new DamageRequest { BasePower = 40f, ComboMultiplier = 1f, TargetArmour = 100 });
            Assert.AreEqual(20, r.Amount);
        }

        [Test]
        public void Resolve_Critical_MultipliesBy1Point5()
        {
            var r = CombatMath.Resolve(new DamageRequest { BasePower = 20f, ComboMultiplier = 1f, IsCritical = true });
            Assert.AreEqual(30, r.Amount);
            Assert.IsTrue(r.WasCritical);
        }

        [Test]
        public void Resolve_EveryHitDoesAtLeastOne()
        {
            var r = CombatMath.Resolve(new DamageRequest { BasePower = 0.01f, ComboMultiplier = 1f, TargetArmour = 100000 });
            Assert.AreEqual(1, r.Amount);
        }

        [Test]
        public void Resolve_VarianceIsClamped()
        {
            var high = CombatMath.Resolve(new DamageRequest { BasePower = 100f, ComboMultiplier = 1f, Variance = 5f });
            var low = CombatMath.Resolve(new DamageRequest { BasePower = 100f, ComboMultiplier = 1f, Variance = -5f });
            Assert.AreEqual(110, high.Amount);
            Assert.AreEqual(90, low.Amount);
        }

        [Test]
        public void Resolve_ResistanceIsClampedTo90Percent()
        {
            var r = CombatMath.Resolve(new DamageRequest { BasePower = 100f, ComboMultiplier = 1f, Resistance = 2f });
            Assert.AreEqual(10, r.Amount);
        }

        [Test]
        public void DepthScaling_FloorZeroIsOne_AndGrows()
        {
            Assert.AreEqual(1f, CombatMath.DepthScaling(0), 0.0001f);
            Assert.AreEqual(1.12f, CombatMath.DepthScaling(1), 0.0001f);
            Assert.AreEqual(1f, CombatMath.DepthScaling(-3), 0.0001f);
        }

        [Test]
        public void SameSeed_SameRolls()
        {
            var a = new Random(1234);
            var b = new Random(1234);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(CombatMath.RollVariance(a), CombatMath.RollVariance(b));
                Assert.AreEqual(CombatMath.RollCrit(a, 0.2f), CombatMath.RollCrit(b, 0.2f));
            }
        }

        [Test]
        public void RollVariance_StaysInRange()
        {
            var rng = new Random(7);
            for (int i = 0; i < 1000; i++)
            {
                float v = CombatMath.RollVariance(rng);
                Assert.GreaterOrEqual(v, -0.1f);
                Assert.LessOrEqual(v, 0.1f);
            }
        }
    }

    public sealed class CombatStateTests
    {
        private static CombatState Fresh() { return new CombatState(100f, 12f); }

        [Test]
        public void LightAttack_CostsStamina()
        {
            var c = Fresh();
            float m = c.TryLightAttack();
            Assert.AreEqual(1f, m);
            Assert.AreEqual(100f - CombatState.LightCost, c.Stamina, 0.001f);
        }

        [Test]
        public void ThirdComboHit_IsBoosted_ThenResets()
        {
            var c = Fresh();
            Assert.AreEqual(1f, c.TryLightAttack());
            Assert.AreEqual(1f, c.TryLightAttack());
            Assert.AreEqual(1.5f, c.TryLightAttack());
            Assert.AreEqual(1f, c.TryLightAttack());
        }

        [Test]
        public void ComboWindow_Expires()
        {
            var c = Fresh();
            c.TryLightAttack();
            c.TryLightAttack();
            c.Tick(CombatState.ComboWindow + 0.01f);
            Assert.AreEqual(0, c.ComboStep);
            Assert.AreEqual(1f, c.TryLightAttack());
        }

        [Test]
        public void NotEnoughStamina_RefusesAttack()
        {
            var c = new CombatState(5f, 0f);
            Assert.AreEqual(0f, c.TryLightAttack());
            Assert.AreEqual(0f, c.TryHeavyAttack());
            Assert.IsFalse(c.TryDodge());
            Assert.AreEqual(5f, c.Stamina);
        }

        [Test]
        public void Dodge_GivesIFrames_ThatExpire()
        {
            var c = Fresh();
            Assert.IsTrue(c.TryDodge());
            Assert.IsTrue(c.IsInvulnerable);
            Assert.AreEqual(0f, c.IncomingHitFraction());
            c.Tick(CombatState.DodgeIFrames + 0.01f);
            Assert.IsFalse(c.IsInvulnerable);
            Assert.AreEqual(1f, c.IncomingHitFraction());
        }

        [Test]
        public void ParryWindow_NegatesThenBlockReduces()
        {
            var c = Fresh();
            c.SetBlocking(true);
            c.BeginParryWindow();
            Assert.AreEqual(0f, c.IncomingHitFraction());
            c.Tick(CombatState.ParryWindow + 0.01f);
            Assert.AreEqual(0.3f, c.IncomingHitFraction());
            c.SetBlocking(false);
            Assert.AreEqual(1f, c.IncomingHitFraction());
        }

        [Test]
        public void Blocking_PausesStaminaRegen()
        {
            var c = new CombatState(100f, 12f);
            c.TryLightAttack();
            c.SetBlocking(true);
            c.Tick(2f);
            Assert.AreEqual(100f - CombatState.LightCost, c.Stamina, 0.001f);
            c.SetBlocking(false);
            c.Tick(2f);
            Assert.Greater(c.Stamina, 100f - CombatState.LightCost);
        }

        [Test]
        public void HeavyAttack_WindsUp_AndStaggerInterrupts()
        {
            var c = Fresh();
            Assert.AreEqual(2f, c.TryHeavyAttack());
            Assert.IsTrue(c.IsWindingUp);
            Assert.AreEqual(0f, c.TryLightAttack()); // no attacks during windup
            c.Stagger();
            Assert.IsFalse(c.IsWindingUp);
        }

        [Test]
        public void Stamina_RegeneratesUpToMax_NeverAbove()
        {
            var c = Fresh();
            c.TryDodge();
            c.Tick(60f);
            Assert.AreEqual(100f, c.Stamina);
        }

        [Test]
        public void RegenDelay_AfterSpending()
        {
            var c = Fresh();
            c.TryLightAttack();
            c.Tick(0.3f); // inside the 0.6s regen delay
            Assert.AreEqual(100f - CombatState.LightCost, c.Stamina, 0.001f);
            c.Tick(1f);
            Assert.Greater(c.Stamina, 100f - CombatState.LightCost);
        }
    }
}
