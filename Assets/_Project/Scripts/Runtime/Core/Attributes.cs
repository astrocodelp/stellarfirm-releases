using System;

namespace Emberfall.Core
{
    /// <summary>Six base attributes. Pure data, no Unity dependency.</summary>
    [Serializable]
    public struct Attributes
    {
        public int Strength;
        public int Dexterity;
        public int Vigor;
        public int Focus;
        public int Will;
        public int Luck;

        public static Attributes Default()
        {
            return new Attributes
            {
                Strength = 10, Dexterity = 10, Vigor = 10,
                Focus = 10, Will = 10, Luck = 10
            };
        }

        public int Total()
        {
            return Strength + Dexterity + Vigor + Focus + Will + Luck;
        }
    }

    public enum AttributeKind { Strength, Dexterity, Vigor, Focus, Will, Luck }

    /// <summary>Derived combat stats from attributes + level. Pure functions.</summary>
    public static class DerivedStats
    {
        public const int MaxLevel = 50;
        public const int PointsPerLevel = 3;
        public const int AttributeCap = 99;

        public static int MaxHealth(Attributes a, int level) { return 80 + 12 * a.Vigor + 2 * level; }
        public static int MaxFocus(Attributes a) { return 30 + 8 * a.Focus; }
        public static int MaxStamina(Attributes a) { return 60 + 4 * a.Vigor + 2 * a.Will; }
        public static int MaxEmber(Attributes a) { return 100 + 10 * a.Will; }

        /// <summary>Crit chance as a fraction 0..0.5.</summary>
        public static float CritChance(Attributes a)
        {
            float c = 0.05f + 0.005f * a.Luck;
            return c > 0.5f ? 0.5f : c;
        }

        /// <summary>Stamina regenerated per second.</summary>
        public static float StaminaRegen(Attributes a) { return 12f + 0.4f * a.Will; }

        /// <summary>Weapon scaling letter S..E to multiplier.</summary>
        public static float ScalingMultiplier(char letter)
        {
            switch (letter)
            {
                case 'S': return 0.08f;
                case 'A': return 0.06f;
                case 'B': return 0.05f;
                case 'C': return 0.04f;
                case 'D': return 0.02f;
                case 'E': return 0.01f;
                default: return 0f;
            }
        }

        /// <summary>Physical power of a weapon given its scaling and the wielder.</summary>
        public static float WeaponPower(float baseDamage, char strLetter, char dexLetter, Attributes a)
        {
            float bonus = 1f
                + ScalingMultiplier(strLetter) * a.Strength
                + ScalingMultiplier(dexLetter) * a.Dexterity;
            return baseDamage * bonus;
        }
    }
}
