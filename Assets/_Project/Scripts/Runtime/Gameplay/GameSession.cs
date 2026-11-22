using System.Collections.Generic;
using Emberfall.Core;
using Emberfall.Data;
using UnityEngine;

namespace Emberfall.Gameplay
{
    /// <summary>
    /// One run: owns the pure systems (progression, ember, quests, inventory),
    /// spawns enemies, hands out XP and loot, saves and loads. Plain C# state
    /// inside a MonoBehaviour shell; no store calls (platform seam only).
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public ItemDatabase Database;
        public PlayerController Player { get; private set; }
        public EmberState Ember { get; private set; }
        public QuestSystem Quests { get; private set; }
        public Inventory Inventory { get; private set; }
        public DungeonFloor Floor { get; private set; }

        public int Level = 1;
        public int Xp;
        public int UnspentPoints;
        public int RunSeed;
        public int FloorIndex;
        public int Kills;
        public int SaveSlot;
        public SettingsData Settings = new SettingsData();

        private readonly List<EnemyBrain> _enemies = new List<EnemyBrain>();
        private readonly float[] _skillCooldowns = new float[3];
        private System.Random _lootRng;
        private int _nextInstanceId = 1;

        public float[] SkillCooldowns { get { return _skillCooldowns; } }
        public int LivingEnemies { get { return _enemies.Count; } }

        public event System.Action<string> Log;              // HUD feed (kill, loot, level)
        public event System.Action PlayerDied;
        public event System.Action<int> LevelledUp;

        public void Begin(PlayerController player, ItemDatabase database, int runSeed)
        {
            Player = player;
            Database = database;
            RunSeed = runSeed;
            FloorIndex = 0;
            Inventory = new Inventory();
            Quests = new QuestSystem();
            _lootRng = new System.Random(unchecked(runSeed * 31 + 7));
            Ember = new EmberState(DerivedStats.MaxEmber(player.Attributes), 100f);
            Floor = DungeonGenerator.Generate(runSeed, FloorIndex);

            player.Init(player.Attributes, Inventory, runSeed);
            player.WeaponLookup = Database != null ? Database.FindWeapon : (System.Func<string, WeaponDefinition>)null;
            player.ItemLookup = Database != null ? Database.FindItem : (System.Func<string, ItemDefinition>)null;
            player.Died += OnPlayerDied;
            player.DealtDamage += OnDealtDamage;

            if (Database != null)
            {
                for (int i = 0; i < Database.Quests.Count; i++)
                {
                    var q = Database.Quests[i];
                    if (q == null) continue;
                    Quests.Register(new QuestDefinition
                    {
                        Id = q.Id,
                        Title = q.Title,
                        Description = q.Description,
                        Objective = q.Objective,
                        TargetId = q.TargetId,
                        RequiredCount = q.RequiredCount,
                        PrerequisiteQuestIds = q.PrerequisiteQuestIds ?? System.Array.Empty<string>(),
                        RewardXp = q.RewardXp,
                        RewardEmber = q.RewardEmber,
                        RewardItemId = q.RewardItemId,
                        RewardWorldFlag = q.RewardWorldFlag
                    });
                }
            }
            GiveStarterWeapon();
        }

        private void Update()
        {
            if (Player == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _skillCooldowns.Length; i++)
                if (_skillCooldowns[i] > 0f) _skillCooldowns[i] -= dt;
            if (Player.IsAlive)
            {
                Ember.TickInHollow(dt);
                float aggression = Ember.AggressionMultiplier;
                for (int i = 0; i < _enemies.Count; i++)
                    if (_enemies[i] != null) _enemies[i].SetAggression(aggression);
            }
        }

        public void RegisterEnemy(EnemyBrain enemy)
        {
            _enemies.Add(enemy);
            enemy.DiedEvent += OnEnemyDied;
        }

        private void OnEnemyDied(EnemyBrain enemy)
        {
            enemy.DiedEvent -= OnEnemyDied;
            _enemies.Remove(enemy);
            Kills++;
            Ember.Gain(EmberState.KillGain);
            int gained = Progression.ApplyXp(ref Level, ref Xp, enemy.Definition.XpReward);
            if (gained > 0)
            {
                UnspentPoints += gained * DerivedStats.PointsPerLevel;
                Player.RecalculateDerived(true);
                Ember.SetCapacity(DerivedStats.MaxEmber(Player.Attributes)
                    + Inventory.AffixTotal(AffixKind.EmberCapacity));
                LevelledUp?.Invoke(Level);
                Log?.Invoke("Level " + Level + " — attribute points available");
            }
            var completed = Quests.OnEvent(QuestObjective.Kill, enemy.Definition.Id, 1);
            for (int i = 0; i < completed.Count; i++) ClaimQuest(completed[i]);
            RollDrop(enemy);
            Log?.Invoke(enemy.Definition.DisplayName + " felled");
            Destroy(enemy.gameObject, 1.5f);
        }

        private void ClaimQuest(string questId)
        {
            QuestReward reward;
            if (!Quests.TryClaimReward(questId, out reward)) return;
            if (reward.Xp > 0)
            {
                int gained = Progression.ApplyXp(ref Level, ref Xp, reward.Xp);
                if (gained > 0) UnspentPoints += gained * DerivedStats.PointsPerLevel;
            }
            if (reward.Ember > 0) Ember.Gain(reward.Ember);
            if (!string.IsNullOrEmpty(reward.ItemId) && Database != null)
            {
                var def = Database.FindItem(reward.ItemId);
                if (def != null)
                {
                    Inventory.Add(new ItemInstance
                    {
                        InstanceId = _nextInstanceId++,
                        DefinitionId = def.Id,
                        Rarity = ItemRarity.Rare
                    });
                    Log?.Invoke("Reward: " + def.DisplayName);
                }
            }
            Log?.Invoke("Quest complete: " + questId);
        }

        private void RollDrop(EnemyBrain enemy)
        {
            if (Database == null || Database.Weapons.Count == 0) return;
            if (_lootRng.NextDouble() > 0.35) return; // 35% drop chance
            var weapon = Database.Weapons[_lootRng.Next(Database.Weapons.Count)];
            if (weapon == null) return;
            var def = Database.FindItem(weapon.Id);
            float luckBonus = (Player.Attributes.Luck - 10) * 0.01f;
            var item = LootSystem.RollItem(_lootRng, def, _nextInstanceId++, FloorIndex, luckBonus);
            Inventory.Add(item);
            Log?.Invoke("Loot: " + def.DisplayName + " (" + RarityRules.Label(item.Rarity) + ")");
        }

        private void OnDealtDamage(int amount, bool crit)
        {
            float lifesteal = Inventory.AffixTotal(AffixKind.Lifesteal);
            if (lifesteal > 0f) Player.Health.Heal(Mathf.Max(1, Mathf.RoundToInt(amount * lifesteal)));
        }

        private void OnPlayerDied()
        {
            Ember.OnDeath();
            Log?.Invoke("You fell. Half your ember waits at your death-mark.");
            PlayerDied?.Invoke();
        }

        public void RespawnPlayer()
        {
            Player.Health.SetMax(Player.Health.Max, true);
            Player.RecalculateDerived(true);
        }

        private void GiveStarterWeapon()
        {
            if (Database == null || Database.Weapons.Count == 0) return;
            var def = Database.FindItem(Database.Weapons[0].Id);
            if (def == null) return;
            var item = new ItemInstance
            {
                InstanceId = _nextInstanceId++,
                DefinitionId = def.Id,
                Rarity = ItemRarity.Common
            };
            Inventory.Add(item);
            Inventory.Equip(item.InstanceId, EquipSlot.Weapon, false);
        }

        // ---- Save / load -------------------------------------------------

        public SaveData ToSaveData()
        {
            var data = new SaveData
            {
                Level = Level,
                Xp = Xp,
                UnspentPoints = UnspentPoints,
                Attributes = Player.Attributes,
                Ember = Ember.Current,
                RunSeed = RunSeed,
                FloorIndex = FloorIndex,
                NextInstanceId = _nextInstanceId,
                Settings = Settings
            };
            data.Items.AddRange(Inventory.Items);
            foreach (var kv in Inventory.Equipped)
                data.Equipment.Add(new EquipEntry { Slot = kv.Key, InstanceId = kv.Value.InstanceId });
            if (Inventory.SecondRing != null)
                data.Equipment.Add(new EquipEntry { Slot = EquipSlot.Ring, SecondRing = true, InstanceId = Inventory.SecondRing.InstanceId });
            foreach (var kv in Quests.States) data.Quests.Add(kv.Value);
            foreach (var flag in Quests.WorldFlags) data.WorldFlags.Add(flag);
            return data;
        }

        public void SaveGame(int slot)
        {
            SaveSlot = slot;
            SaveSystem.Save(slot, ToSaveData());
            Log?.Invoke("Saved to slot " + (slot + 1));
        }

        /// <summary>Loads a slot into this session. Returns false when the slot is empty.</summary>
        public bool LoadGame(int slot)
        {
            var data = SaveSystem.Load(slot);
            if (data == null) return false;
            SaveSlot = slot;
            Level = data.Level;
            Xp = data.Xp;
            UnspentPoints = data.UnspentPoints;
            RunSeed = data.RunSeed;
            FloorIndex = data.FloorIndex;
            Settings = data.Settings ?? new SettingsData();
            _nextInstanceId = data.NextInstanceId;
            Floor = DungeonGenerator.Generate(RunSeed, FloorIndex);

            Player.Attributes = data.Attributes.Total() > 0 ? data.Attributes : Attributes.Default();
            Player.RecalculateDerived(true);
            Ember.SetCapacity(DerivedStats.MaxEmber(Player.Attributes));
            Ember.SetCurrent(data.Ember);

            for (int i = Inventory.Items.Count - 1; i >= 0; i--)
                Inventory.Remove(Inventory.Items[i].InstanceId);
            for (int i = 0; i < data.Items.Count; i++) Inventory.Add(data.Items[i]);
            for (int i = 0; i < data.Equipment.Count; i++)
            {
                var e = data.Equipment[i];
                Inventory.Equip(e.InstanceId, e.Slot, e.SecondRing);
            }
            for (int i = 0; i < data.Quests.Count; i++) Quests.LoadState(data.Quests[i]);
            for (int i = 0; i < data.WorldFlags.Count; i++) Quests.SetWorldFlag(data.WorldFlags[i]);
            Player.RecalculateDerived(false);
            Log?.Invoke("Loaded slot " + (slot + 1));
            return true;
        }

        private void OnDestroy()
        {
            if (Player != null)
            {
                Player.Died -= OnPlayerDied;
                Player.DealtDamage -= OnDealtDamage;
            }
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null) _enemies[i].DiedEvent -= OnEnemyDied;
            Log = null;
            PlayerDied = null;
            LevelledUp = null;
        }
    }
}
