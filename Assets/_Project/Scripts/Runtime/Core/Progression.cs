using System;

namespace Emberfall.Core
{
    /// <summary>XP curve and leveling. Pure functions, unit-tested.</summary>
    public static class Progression
    {
        /// <summary>Total XP needed to go from level n to n+1. Level 1 -> 2 costs 75.</summary>
        public static int XpToNext(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            return 50 * level + 25 * level * level;
        }

        /// <summary>
        /// Applies XP, returning levels gained. Mutates level and carries remainder XP.
        /// Stops at MaxLevel; excess XP is discarded at cap.
        /// </summary>
        public static int ApplyXp(ref int level, ref int xp, int gained)
        {
            if (gained <= 0) return 0; // no-op for zero/negative; callers can't crash on bad tuning data
            if (level < 1) level = 1;
            int levelsGained = 0;
            xp += gained;
            while (level < DerivedStats.MaxLevel && xp >= XpToNext(level))
            {
                xp -= XpToNext(level);
                level++;
                levelsGained++;
            }
            if (level >= DerivedStats.MaxLevel) xp = 0;
            return levelsGained;
        }

        /// <summary>Unspent attribute points earned through leveling (3 per level after 1).</summary>
        public static int PointsEarned(int level)
        {
            return (level - 1) * DerivedStats.PointsPerLevel;
        }

        /// <summary>True when a point may be spent on the attribute.</summary>
        public static bool CanSpend(Attributes a, AttributeKind kind, int unspentPoints)
        {
            if (unspentPoints <= 0) return false;
            int current;
            switch (kind)
            {
                case AttributeKind.Strength: current = a.Strength; break;
                case AttributeKind.Dexterity: current = a.Dexterity; break;
                case AttributeKind.Vigor: current = a.Vigor; break;
                case AttributeKind.Focus: current = a.Focus; break;
                case AttributeKind.Will: current = a.Will; break;
                case AttributeKind.Luck: current = a.Luck; break;
                default: return false;
            }
            return current < DerivedStats.AttributeCap;
        }

        /// <summary>Spends one point. Returns false (and changes nothing) when not allowed.</summary>
        public static bool Spend(ref Attributes a, AttributeKind kind, ref int unspentPoints)
        {
            if (!CanSpend(a, kind, unspentPoints)) return false;
            switch (kind)
            {
                case AttributeKind.Strength: a.Strength++; break;
                case AttributeKind.Dexterity: a.Dexterity++; break;
                case AttributeKind.Vigor: a.Vigor++; break;
                case AttributeKind.Focus: a.Focus++; break;
                case AttributeKind.Will: a.Will++; break;
                case AttributeKind.Luck: a.Luck++; break;
            }
            unspentPoints--;
            return true;
        }
    }
}
