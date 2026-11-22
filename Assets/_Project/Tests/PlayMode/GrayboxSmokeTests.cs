using System.Collections;
using Emberfall.Data;
using Emberfall.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Emberfall.Tests.PlayMode
{
    /// <summary>
    /// Boots the whole graybox from code (no scene asset needed) and verifies
    /// the loop: world built, player alive, enemies spawned, damage lands,
    /// save/load works. Runs headless in CI (PlayMode, Standalone player).
    /// </summary>
    public sealed class GrayboxSmokeTests
    {
        private GameObject _root;
        private GrayboxBootstrap _bootstrap;
        private GameSession _session;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Bootstrap");
            _bootstrap = _root.AddComponent<GrayboxBootstrap>();
            _bootstrap.RunSeed = 12345;
            yield return null; // Start runs
            _session = _root.GetComponent<GameSession>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_BuildsPlayableWorld()
        {
            Assert.IsNotNull(_session, "session created");
            Assert.IsNotNull(_session.Player, "player created");
            Assert.IsTrue(_session.Player.IsAlive);
            Assert.Greater(_session.Player.Health.Max, 0);
            Assert.Greater(_session.LivingEnemies, 0, "wave spawned");
            Assert.IsNotNull(GameObject.FindGameObjectWithTag("MainCamera"));
            Assert.IsNotNull(_session.Floor);
            Assert.IsTrue(Core.DungeonGenerator.IsFullyConnected(_session.Floor));
            yield break;
        }

        [UnityTest]
        public IEnumerator PlayerAttack_DamagesEnemy_InFront()
        {
            // Put one enemy directly in front of the player, kill the rest.
            var enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
            EnemyBrain victim = enemies[0];
            for (int i = 1; i < enemies.Length; i++) enemies[i].Health.ApplyDamage(99999);
            yield return null;
            victim.transform.position = _session.Player.transform.position + Vector3.forward * 1.2f;
            int before = victim.Health.Current;
            Physics.SyncTransforms(); // autoSyncTransforms is off; make the teleport visible to queries
            _session.Player.transform.rotation = Quaternion.identity; // face +Z
            // Swing via the public skill path (deterministic, no Input API in tests).
            var skill = _session.Database.Skills[0]; // Ember Slash, range 3
            bool cast = _session.Player.TryCastSkill(skill, _session.SkillCooldowns, 0);
            Assert.IsTrue(cast, "skill cast with full focus");
            Assert.Less(victim.Health.Current, before, "damage landed");
            yield break;
        }

        [UnityTest]
        public IEnumerator EnemyKill_GrantsXpAndEmber()
        {
            var enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
            int xpBefore = _session.Xp;
            float emberBefore = _session.Ember.Current;
            enemies[0].Health.ApplyDamage(99999);
            yield return null;
            Assert.AreEqual(1, _session.Kills);
            // First husk kill grants 20 XP; the level-1 curve needs 75, so no level-up here.
            Assert.Greater(_session.Xp, xpBefore, "xp gained");
            Assert.GreaterOrEqual(_session.Ember.Current, emberBefore - 1f, "ember gained minus drain");
        }

        [UnityTest]
        public IEnumerator SaveLoad_RestoresLevelAndInventory()
        {
            _session.SaveGame(2);
            int level = _session.Level;
            int itemCount = _session.Inventory.Items.Count;
            _session.Player.Health.ApplyDamage(50);
            Assert.IsTrue(_session.LoadGame(2));
            Assert.AreEqual(level, _session.Level);
            Assert.AreEqual(itemCount, _session.Inventory.Items.Count);
            Assert.AreEqual(_session.Player.Health.Max, _session.Player.Health.Current);
            yield break;
        }

        [UnityTest]
        public IEnumerator PlayerDeath_DropsEmberMark_AndRespawnWorks()
        {
            float before = _session.Ember.Current;
            _session.Player.Health.ApplyDamage(99999);
            yield return null;
            Assert.IsFalse(_session.Player.IsAlive);
            Assert.IsTrue(_session.Ember.HasDeathMark);
            Assert.AreEqual(before * 0.5f, _session.Ember.DeathMarkEmber, 1f);
            _session.RespawnPlayer();
            Assert.IsTrue(_session.Player.IsAlive);
            Assert.AreEqual(_session.Player.Health.Max, _session.Player.Health.Current);
        }
    }
}
