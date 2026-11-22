using System.Collections.Generic;
using UnityEngine;

namespace Emberfall.Data
{
    /// <summary>
    /// One database asset holds every definition. Saves reference string ids,
    /// so assets can move or be re-imported without breaking saves.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Emberfall/Item Database")]
    public sealed class ItemDatabase : ScriptableObject
    {
        public List<WeaponDefinition> Weapons = new List<WeaponDefinition>();
        public List<ArmourDefinition> Armour = new List<ArmourDefinition>();
        public List<EnemyDefinition> Enemies = new List<EnemyDefinition>();
        public List<SkillDefinition> Skills = new List<SkillDefinition>();
        public List<QuestAsset> Quests = new List<QuestAsset>();

        public Core.ItemDefinition FindItem(string id)
        {
            for (int i = 0; i < Weapons.Count; i++)
            {
                var w = Weapons[i];
                if (w != null && w.Id == id)
                {
                    return new Core.ItemDefinition
                    {
                        Id = w.Id,
                        DisplayName = w.DisplayName,
                        Slot = Core.EquipSlot.Weapon,
                        BaseDamage = w.BaseDamage,
                        StrScaling = Letter(w.StrScaling),
                        DexScaling = Letter(w.DexScaling),
                        AttackSpeed = w.AttackSpeed
                    };
                }
            }
            for (int i = 0; i < Armour.Count; i++)
            {
                var a = Armour[i];
                if (a != null && a.Id == id)
                {
                    return new Core.ItemDefinition
                    {
                        Id = a.Id,
                        DisplayName = a.DisplayName,
                        Slot = a.Slot,
                        BaseArmour = a.BaseArmour
                    };
                }
            }
            return null;
        }

        public WeaponDefinition FindWeapon(string id)
        {
            for (int i = 0; i < Weapons.Count; i++)
                if (Weapons[i] != null && Weapons[i].Id == id) return Weapons[i];
            return null;
        }

        public EnemyDefinition FindEnemy(string id)
        {
            for (int i = 0; i < Enemies.Count; i++)
                if (Enemies[i] != null && Enemies[i].Id == id) return Enemies[i];
            return null;
        }

        private static char Letter(string s)
        {
            return string.IsNullOrEmpty(s) ? 'E' : s[0];
        }
    }
}
