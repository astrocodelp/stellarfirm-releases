using System.IO;
using Emberfall.Core;
using NUnit.Framework;

namespace Emberfall.Tests.EditMode
{
    public sealed class SaveSystemTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "emberfall_tests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        private static SaveData Sample()
        {
            var data = new SaveData
            {
                Level = 7,
                Xp = 123,
                UnspentPoints = 4,
                Attributes = Attributes.Default(),
                Ember = 55.5f,
                RunSeed = 4242,
                FloorIndex = 2,
                NextInstanceId = 9
            };
            data.Attributes.Strength = 15;
            var item = new ItemInstance { InstanceId = 3, DefinitionId = "wpn_ember_blade", Rarity = ItemRarity.Epic };
            item.Affixes.Add(new Affix { Kind = AffixKind.CritChance, Value = 0.05f });
            data.Items.Add(item);
            data.Equipment.Add(new EquipEntry { Slot = EquipSlot.Weapon, InstanceId = 3 });
            data.Quests.Add(new QuestState { QuestId = "qst_first_ember", Status = QuestStatus.Completed, Counter = 3 });
            data.WorldFlags.Add("flag_first_ember");
            data.Settings.TextScale = 1.5f;
            data.Settings.ToggleBlock = true;
            return data;
        }

        [Test]
        public void RoundTrip_PreservesEverything()
        {
            string json = UnityEngine.JsonUtility.ToJson(Sample(), true);
            var loaded = SaveSystem.FromJson(json);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(7, loaded.Level);
            Assert.AreEqual(123, loaded.Xp);
            Assert.AreEqual(15, loaded.Attributes.Strength);
            Assert.AreEqual(55.5f, loaded.Ember, 0.001f);
            Assert.AreEqual(4242, loaded.RunSeed);
            Assert.AreEqual(1, loaded.Items.Count);
            Assert.AreEqual(ItemRarity.Epic, loaded.Items[0].Rarity);
            Assert.AreEqual(AffixKind.CritChance, loaded.Items[0].Affixes[0].Kind);
            Assert.AreEqual(EquipSlot.Weapon, loaded.Equipment[0].Slot);
            Assert.AreEqual(QuestStatus.Completed, loaded.Quests[0].Status);
            Assert.AreEqual(3, loaded.Quests[0].Counter);
            CollectionAssert.Contains(loaded.WorldFlags, "flag_first_ember");
            Assert.AreEqual(1.5f, loaded.Settings.TextScale, 0.001f);
            Assert.IsTrue(loaded.Settings.ToggleBlock);
        }

        [Test]
        public void FromJson_EmptyOrNull_ReturnsNull()
        {
            Assert.IsNull(SaveSystem.FromJson(null));
            Assert.IsNull(SaveSystem.FromJson(""));
        }

        [Test]
        public void Migration_Version0_GetsDefaultsAndNewVersion()
        {
            var old = Sample();
            old.Version = 0;
            old.Attributes = default(Attributes); // all zero — pre-release bug
            old.Settings = null;
            string json = UnityEngine.JsonUtility.ToJson(old, true);
            var loaded = SaveSystem.FromJson(json);
            Assert.AreEqual(SaveSystem.CurrentVersion, loaded.Version);
            Assert.AreEqual(Attributes.Default().Total(), loaded.Attributes.Total());
            Assert.IsNotNull(loaded.Settings);
            Assert.AreEqual(1f, loaded.Settings.TextScale, 0.001f);
        }

        [Test]
        public void AtomicWrite_ReplacesExisting_AndLeavesNoTemp()
        {
            string path = Path.Combine(_tempDir, "slot.json");
            SaveSystem.WriteAtomic(path, "first");
            SaveSystem.WriteAtomic(path, "second");
            Assert.AreEqual("second", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
            Assert.IsTrue(File.Exists(path + ".bak"));
            Assert.AreEqual("first", File.ReadAllText(path + ".bak"));
        }

        [Test]
        public void AtomicWrite_CreatesMissingDirectory()
        {
            string path = Path.Combine(_tempDir, "nested", "deep", "slot.json");
            SaveSystem.WriteAtomic(path, "data");
            Assert.AreEqual("data", File.ReadAllText(path));
        }

        [Test]
        public void SlotPath_RejectsOutOfRangeSlots()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SaveSystem.SlotPath(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SaveSystem.SlotPath(SaveSystem.SlotCount));
        }

        [Test]
        public void SlotSaveLoad_RoundTripsThroughDisk()
        {
            // Uses Application.persistentDataPath — valid in EditMode tests.
            int slot = SaveSystem.SlotCount - 1; // last slot, least likely used by a real player
            bool hadSave = SaveSystem.SlotExists(slot);
            string backup = hadSave ? File.ReadAllText(SaveSystem.SlotPath(slot)) : null;
            try
            {
                SaveSystem.Save(slot, Sample());
                Assert.IsTrue(SaveSystem.SlotExists(slot));
                var loaded = SaveSystem.Load(slot);
                Assert.AreEqual(7, loaded.Level);
                Assert.AreEqual(SaveSystem.CurrentVersion, loaded.Version);
                Assert.IsNotEmpty(loaded.SavedAtUtc);
            }
            finally
            {
                if (hadSave) File.WriteAllText(SaveSystem.SlotPath(slot), backup);
                else File.Delete(SaveSystem.SlotPath(slot));
            }
        }
    }
}
