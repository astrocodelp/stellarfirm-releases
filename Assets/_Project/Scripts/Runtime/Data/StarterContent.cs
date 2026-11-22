using Emberfall.Core;
using Emberfall.Data;
using UnityEngine;

namespace Emberfall.Data
{
    /// <summary>
    /// Code-built starter content so the graybox is playable before any
    /// ScriptableObject authoring. The Editor menu "Emberfall → Create Starter
    /// Content" persists the same definitions as assets. Ids are stable forever.
    /// </summary>
    public static class StarterContent
    {
        public static ItemDatabase CreateRuntimeDatabase()
        {
            var db = ScriptableObject.CreateInstance<ItemDatabase>();
            db.name = "StarterDatabase (runtime)";
            Fill(db);
            return db;
        }

        public static void Fill(ItemDatabase db)
        {
            db.Weapons.Add(Weapon("wpn_ember_blade", "Ember Blade", "The last blade lit at the First Hearth.", 12f, 'C', 'D', 1f));
            db.Weapons.Add(Weapon("wpn_hollow_pike", "Hollow Pike", "Long reach, slow recovery.", 16f, 'B', 'E', 0.8f));
            db.Weapons.Add(Weapon("wpn_cinder_daggers", "Cinder Daggers", "Fast strikes that reward dexterity.", 8f, 'E', 'B', 1.4f));

            db.Armour.Add(ArmourPiece("arm_ashen_helm", "Ashen Helm", EquipSlot.Helm, 8));
            db.Armour.Add(ArmourPiece("arm_emberbearer_mail", "Emberbearer Mail", EquipSlot.Chest, 18));
            db.Armour.Add(ArmourPiece("arm_cinder_gloves", "Cinder Gloves", EquipSlot.Gloves, 5));
            db.Armour.Add(ArmourPiece("arm_wanderer_boots", "Wanderer Boots", EquipSlot.Boots, 6));
            db.Armour.Add(ArmourPiece("arm_signet_of_will", "Signet of Will", EquipSlot.Ring, 0));
            db.Armour.Add(ArmourPiece("arm_hollow_amulet", "Hollow Amulet", EquipSlot.Amulet, 0));

            db.Enemies.Add(Enemy("enm_husk", "Hollow Husk", 40, 8f, 5, 2.6f, 0.7f, 20, false));
            db.Enemies.Add(Enemy("enm_ash_hound", "Ash Hound", 28, 6f, 2, 4.2f, 0.5f, 16, false));
            db.Enemies.Add(Enemy("enm_grief_knight", "Grief Knight", 120, 14f, 20, 2.0f, 1.0f, 90, true));

            db.Skills.Add(Skill("skl_ember_slash", "Ember Slash", "A burning arc that strikes every foe ahead.", SkillEffect.EmberSlash, 15f, 4f, 1.2f, 3f));
            db.Skills.Add(Skill("skl_cinder_step", "Cinder Step", "Dash through ash; recovers stamina.", SkillEffect.CinderStep, 10f, 3f, 1f, 5f));
            db.Skills.Add(Skill("skl_ward_of_cinders", "Ward of Cinders", "Warm light knits wounds closed.", SkillEffect.WardOfCinders, 25f, 10f, 1.5f, 0f));

            db.Quests.Add(Quest("qst_first_ember", "First Ember",
                "The Hollow stirs. Fell three Husks to kindle your ember.",
                QuestObjective.Kill, "enm_husk", 3, null, 60, 20, null, "flag_first_ember"));
            db.Quests.Add(Quest("qst_grief_of_the_crown", "Grief of the Crown",
                "A knight still guards a shard of the dead crown. End its watch.",
                QuestObjective.Kill, "enm_grief_knight", 1,
                new[] { "qst_first_ember" }, 200, 50, "arm_hollow_amulet", "flag_shard_one"));
        }

        private static WeaponDefinition Weapon(string id, string name, string desc, float dmg, char str, char dex, float speed)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            w.Id = id; w.DisplayName = name; w.Description = desc;
            w.BaseDamage = dmg; w.StrScaling = str.ToString(); w.DexScaling = dex.ToString();
            w.AttackSpeed = speed;
            return w;
        }

        private static ArmourDefinition ArmourPiece(string id, string name, EquipSlot slot, int armour)
        {
            var a = ScriptableObject.CreateInstance<ArmourDefinition>();
            a.Id = id; a.DisplayName = name; a.Slot = slot; a.BaseArmour = armour;
            return a;
        }

        private static EnemyDefinition Enemy(string id, string name, int hp, float dmg, int armour, float speed, float telegraph, int xp, bool elite)
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            e.Id = id; e.DisplayName = name; e.MaxHealth = hp; e.BaseDamage = dmg;
            e.Armour = armour; e.MoveSpeed = speed; e.TelegraphTime = telegraph;
            e.XpReward = xp; e.IsElite = elite;
            return e;
        }

        private static SkillDefinition Skill(string id, string name, string desc, SkillEffect effect, float cost, float cd, float power, float range)
        {
            var s = ScriptableObject.CreateInstance<SkillDefinition>();
            s.Id = id; s.DisplayName = name; s.Description = desc; s.Effect = effect;
            s.FocusCost = cost; s.Cooldown = cd; s.Power = power; s.Range = range;
            return s;
        }

        private static QuestAsset Quest(string id, string title, string desc, QuestObjective objective, string target, int count, string[] prereq, int xp, int ember, string item, string flag)
        {
            var q = ScriptableObject.CreateInstance<QuestAsset>();
            q.Id = id; q.Title = title; q.Description = desc; q.Objective = objective;
            q.TargetId = target; q.RequiredCount = count;
            q.PrerequisiteQuestIds = prereq; q.RewardXp = xp; q.RewardEmber = ember;
            q.RewardItemId = item; q.RewardWorldFlag = flag;
            return q;
        }
    }
}
