using Emberfall.Core;
using NUnit.Framework;

namespace Emberfall.Tests.EditMode
{
    public sealed class ProgressionTests
    {
        [Test]
        public void XpCurve_IsMonotonic()
        {
            for (int level = 1; level < DerivedStats.MaxLevel; level++)
                Assert.Greater(Progression.XpToNext(level + 1), Progression.XpToNext(level));
        }

        [Test]
        public void ApplyXp_LevelsUp_AndCarriesOverflow()
        {
            int level = 1, xp = 0;
            int need = Progression.XpToNext(1);
            int gained = Progression.ApplyXp(ref level, ref xp, need + 5);
            Assert.AreEqual(1, gained);
            Assert.AreEqual(2, level);
            Assert.AreEqual(5, xp);
        }

        [Test]
        public void ApplyXp_MultiLevelJump()
        {
            int level = 1, xp = 0;
            int total = Progression.XpToNext(1) + Progression.XpToNext(2) + Progression.XpToNext(3);
            int gained = Progression.ApplyXp(ref level, ref xp, total);
            Assert.AreEqual(3, gained);
            Assert.AreEqual(4, level);
            Assert.AreEqual(0, xp);
        }

        [Test]
        public void ApplyXp_StopsAtMaxLevel()
        {
            int level = DerivedStats.MaxLevel, xp = 0;
            int gained = Progression.ApplyXp(ref level, ref xp, 1000000);
            Assert.AreEqual(0, gained);
            Assert.AreEqual(DerivedStats.MaxLevel, level);
        }

        [Test]
        public void ApplyXp_NegativeOrZero_DoesNothing()
        {
            int level = 3, xp = 10;
            Assert.AreEqual(0, Progression.ApplyXp(ref level, ref xp, 0));
            Assert.AreEqual(0, Progression.ApplyXp(ref level, ref xp, -50));
            Assert.AreEqual(3, level);
            Assert.AreEqual(10, xp);
        }

        [Test]
        public void Spend_RequiresPoints_AndRespectsCap()
        {
            var a = Attributes.Default();
            int points = 0;
            Assert.IsFalse(Progression.Spend(ref a, AttributeKind.Strength, ref points));
            points = 2;
            Assert.IsTrue(Progression.Spend(ref a, AttributeKind.Strength, ref points));
            Assert.AreEqual(11, a.Strength);
            Assert.AreEqual(1, points);
            a.Strength = DerivedStats.AttributeCap;
            Assert.IsFalse(Progression.Spend(ref a, AttributeKind.Strength, ref points));
            Assert.AreEqual(1, points); // unchanged on refusal
        }

        [Test]
        public void DerivedStats_GrowWithAttributes()
        {
            var weak = Attributes.Default();
            var strong = weak;
            strong.Vigor += 10;
            Assert.Greater(DerivedStats.MaxHealth(strong, 1), DerivedStats.MaxHealth(weak, 1));
            Assert.Greater(DerivedStats.MaxStamina(strong), DerivedStats.MaxStamina(weak));
        }

        [Test]
        public void CritChance_NeverExceedsHalf()
        {
            var a = Attributes.Default();
            a.Luck = DerivedStats.AttributeCap;
            a.Dexterity = DerivedStats.AttributeCap;
            Assert.LessOrEqual(DerivedStats.CritChance(a), 0.5f);
            Assert.GreaterOrEqual(DerivedStats.CritChance(a), 0f);
        }
    }

    public sealed class EmberStateTests
    {
        [Test]
        public void Drain_NeverBelowZero()
        {
            var e = new EmberState(100f, 2f);
            e.TickInHollow(10f);
            Assert.AreEqual(0f, e.Current);
        }

        [Test]
        public void Gain_ClampsToCapacity()
        {
            var e = new EmberState(100f, 90f);
            e.Gain(EmberState.KillGain * 10);
            Assert.AreEqual(100f, e.Current);
        }

        [Test]
        public void LowEmber_RaisesAggression()
        {
            var e = new EmberState(100f, 20f);
            Assert.IsTrue(e.IsLow);
            Assert.AreEqual(1.2f, e.AggressionMultiplier, 0.0001f);
            e.Gain(50f);
            Assert.IsFalse(e.IsLow);
            Assert.AreEqual(1f, e.AggressionMultiplier);
        }

        [Test]
        public void Death_DropsHalf_AndRecoveryRestoresIt()
        {
            var e = new EmberState(200f, 80f);
            e.OnDeath();
            Assert.AreEqual(40f, e.Current);
            Assert.IsTrue(e.HasDeathMark);
            float recovered = e.RecoverDeathMark();
            Assert.AreEqual(40f, recovered);
            Assert.AreEqual(80f, e.Current);
            Assert.IsFalse(e.HasDeathMark);
            Assert.AreEqual(0f, e.RecoverDeathMark()); // second touch: nothing
        }

        [Test]
        public void SecondDeath_ReplacesOldMark()
        {
            var e = new EmberState(200f, 80f);
            e.OnDeath();       // mark 40, carry 40
            e.OnDeath();       // mark 20, carry 20 — old 40 lost
            Assert.AreEqual(20f, e.RecoverDeathMark());
            Assert.AreEqual(40f, e.Current);
        }

        [Test]
        public void TrySpend_RefusesWhenUnaffordable()
        {
            var e = new EmberState(100f, 10f);
            Assert.IsFalse(e.TrySpend(11f));
            Assert.AreEqual(10f, e.Current);
            Assert.IsTrue(e.TrySpend(10f));
            Assert.AreEqual(0f, e.Current);
        }
    }
}
