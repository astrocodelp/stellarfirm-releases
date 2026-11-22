using System;
using System.IO;
using Emberfall.Core;
using NUnit.Framework;

namespace Emberfall.Tests.EditMode
{
    public sealed class DungeonGeneratorTests
    {
        [Test]
        public void SameSeed_SameFloor()
        {
            var a = DungeonGenerator.Generate(42, 3);
            var b = DungeonGenerator.Generate(42, 3);
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
            {
                Assert.AreEqual(a.Rooms[i].Kind, b.Rooms[i].Kind);
                Assert.AreEqual(a.Rooms[i].EncounterSeed, b.Rooms[i].EncounterSeed);
                CollectionAssert.AreEqual(a.Rooms[i].Connections, b.Rooms[i].Connections);
            }
        }

        [Test]
        public void DifferentSeed_DifferentFloor()
        {
            var a = DungeonGenerator.Generate(1, 0);
            var b = DungeonGenerator.Generate(2, 0);
            bool anyDifference = a.Rooms.Count != b.Rooms.Count;
            if (!anyDifference)
                for (int i = 0; i < a.Rooms.Count; i++)
                    if (a.Rooms[i].EncounterSeed != b.Rooms[i].EncounterSeed) { anyDifference = true; break; }
            Assert.IsTrue(anyDifference);
        }

        [Test]
        public void EveryFloor_IsFullyConnected()
        {
            for (int seed = 0; seed < 200; seed++)
                for (int floor = 0; floor < 5; floor++)
                    Assert.IsTrue(DungeonGenerator.IsFullyConnected(DungeonGenerator.Generate(seed, floor)),
                        "seed " + seed + " floor " + floor);
        }

        [Test]
        public void Floor_HasEntryAndBoss_AndMiddleRooms()
        {
            var f = DungeonGenerator.Generate(9, 0);
            Assert.AreEqual(RoomKind.Entry, f.Rooms[f.EntryRoomId].Kind);
            Assert.AreEqual(RoomKind.BossGate, f.Rooms[f.BossRoomId].Kind);
            Assert.GreaterOrEqual(f.Rooms.Count, 6);  // entry + 4 middle + boss
            Assert.LessOrEqual(f.Rooms.Count, 15);    // 8 middle + 8 branches + entry + boss bound
        }

        [Test]
        public void Connections_AreSymmetric_AndNoSelfLinks()
        {
            var f = DungeonGenerator.Generate(77, 2);
            foreach (var room in f.Rooms)
                foreach (int other in room.Connections)
                {
                    Assert.AreNotEqual(room.Id, other);
                    CollectionAssert.Contains(f.Rooms[other].Connections, room.Id);
                }
        }

        [Test]
        public void NegativeFloor_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DungeonGenerator.Generate(1, -1));
        }
    }

    public sealed class QuestSystemTests
    {
        private static QuestDefinition KillQuest(string id, string target, int count, string[] prereq = null)
        {
            return new QuestDefinition
            {
                Id = id, Title = id, Objective = QuestObjective.Kill, TargetId = target,
                RequiredCount = count,
                PrerequisiteQuestIds = prereq ?? Array.Empty<string>(),
                RewardXp = 50, RewardEmber = 10, RewardWorldFlag = "flag_" + id
            };
        }

        [Test]
        public void NewQuest_NoPrereq_IsActive()
        {
            var q = new QuestSystem();
            q.Register(KillQuest("a", "husk", 3));
            Assert.AreEqual(QuestStatus.Active, q.States["a"].Status);
        }

        [Test]
        public void PrereqQuest_StaysLocked_UtilRewardClaimed()
        {
            var q = new QuestSystem();
            q.Register(KillQuest("a", "husk", 1));
            q.Register(KillQuest("b", "knight", 1, new[] { "a" }));
            Assert.AreEqual(QuestStatus.Locked, q.States["b"].Status);
            var done = q.OnEvent(QuestObjective.Kill, "husk", 1);
            CollectionAssert.Contains(done, "a");
            Assert.AreEqual(QuestStatus.Locked, q.States["b"].Status); // completed but not rewarded
            QuestReward reward;
            Assert.IsTrue(q.TryClaimReward("a", out reward));
            Assert.AreEqual(QuestStatus.Active, q.States["b"].Status);
            CollectionAssert.Contains(q.WorldFlags, "flag_a");
        }

        [Test]
        public void Counter_ClampsAtRequired()
        {
            var q = new QuestSystem();
            q.Register(KillQuest("a", "husk", 3));
            q.OnEvent(QuestObjective.Kill, "husk", 10);
            Assert.AreEqual(3, q.States["a"].Counter);
            Assert.AreEqual(QuestStatus.Completed, q.States["a"].Status);
        }

        [Test]
        public void WrongTargetOrKind_DoesNotAdvance()
        {
            var q = new QuestSystem();
            q.Register(KillQuest("a", "husk", 3));
            q.OnEvent(QuestObjective.Kill, "hound", 3);
            q.OnEvent(QuestObjective.Collect, "husk", 3);
            Assert.AreEqual(0, q.States["a"].Counter);
        }

        [Test]
        public void Reward_ClaimedExactlyOnce()
        {
            var q = new QuestSystem();
            q.Register(KillQuest("a", "husk", 1));
            q.OnEvent(QuestObjective.Kill, "husk", 1);
            QuestReward reward;
            Assert.IsTrue(q.TryClaimReward("a", out reward));
            Assert.AreEqual(50, reward.Xp);
            Assert.IsFalse(q.TryClaimReward("a", out reward));
        }

        [Test]
        public void LoadState_RestoresProgress()
        {
            var q = new QuestSystem();
            q.Register(KillQuest("a", "husk", 5));
            q.LoadState(new QuestState { QuestId = "a", Status = QuestStatus.Active, Counter = 4 });
            var done = q.OnEvent(QuestObjective.Kill, "husk", 1);
            CollectionAssert.Contains(done, "a");
        }
    }
}
