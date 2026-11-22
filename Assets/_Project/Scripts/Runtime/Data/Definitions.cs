using UnityEngine;

namespace Emberfall.Data
{
    public enum SkillEffect { EmberSlash, CinderStep, WardOfCinders }

    [CreateAssetMenu(fileName = "Skill_", menuName = "Emberfall/Skill")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Tooltip("Stable id used in saves and tests. Never rename after release.")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public SkillEffect Effect;
        public float FocusCost = 15f;
        public float Cooldown = 4f;
        public float Power = 1f;         // damage multiplier or shield amount factor
        public float Range = 2.5f;
    }

    [CreateAssetMenu(fileName = "Weapon_", menuName = "Emberfall/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public float BaseDamage = 12f;
        public int BaseArmour;
        [Tooltip("Scaling letter S, A, B, C, D or E.")] public string StrScaling = "C";
        public string DexScaling = "E";
        public float AttackSpeed = 1f;
        public float StaminaCostLight = 10f;
    }

    [CreateAssetMenu(fileName = "Armour_", menuName = "Emberfall/Armour")]
    public sealed class ArmourDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Core.EquipSlot Slot = Core.EquipSlot.Chest;
        public int BaseArmour = 10;
    }

    [CreateAssetMenu(fileName = "Enemy_", menuName = "Emberfall/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public int MaxHealth = 40;
        public float BaseDamage = 8f;
        public int Armour = 5;
        public float MoveSpeed = 2.5f;
        public float AttackRange = 1.6f;
        [Tooltip("Telegraph time before the hit lands. Never below 0.45s (readability).")]
        public float TelegraphTime = 0.7f;
        public float AttackCooldown = 1.6f;
        public int XpReward = 20;
        public bool IsElite;
    }

    [CreateAssetMenu(fileName = "Quest_", menuName = "Emberfall/Quest")]
    public sealed class QuestAsset : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Description;
        public Core.QuestObjective Objective;
        public string TargetId;
        public int RequiredCount = 1;
        public string[] PrerequisiteQuestIds;
        public int RewardXp;
        public int RewardEmber;
        public string RewardItemId;
        public string RewardWorldFlag;
    }
}
