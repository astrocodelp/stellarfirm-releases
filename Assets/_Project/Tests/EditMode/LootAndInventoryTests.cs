using System;
using System.Collections.Generic;
using Emberfall.Core;
using NUnit.Framework;

namespace Emberfall.Tests.EditMode
{
    public sealed class LootTests
    {
        private static ItemDefinition Blade()
        {
            return new ItemDefinition { Id = "wpn_test", DisplayName = "Test Blade", Slot = EquipSlot.Weapon, BaseDamage = 10f };
        }

        [Test]
        public void AffixCounts_MatchRarity()
        {
            var rng = new Random(5);
            for (int i = 0; i < 200; i++)
            {
                var item = LootSystem.RollItem(rng, Blade(), i, 2, 0f);
                Assert.AreEqual(RarityRules.AffixCount(item.Rarity), item.Affixes.Count);
                Assert.AreEqual("wpn_test", item.DefinitionId);
                Assert.AreEqual(i, item.InstanceId);
            }
        }

        [Test]
        public void AffixKinds_AreDistinct()
        {
            var rng = new Random(11);
            for (int i = 0; i < 200; i++)
            {
                var item = LootSystem.RollItem(rng, Blade(), i, 5, 0.5f);
                var seen = new HashSet<AffixKind>();
                foreach (var affix in item.Affixes)
                    Assert.IsTrue(seen.Add(affix.Kind), "duplicate affix " + affix.Kind);
            }
        }

        [Test]
        public void SameSeed_SameLoot()
        {
            var a = new Random(99);
            var b = new Random(99);
            for (int i = 0; i < 50; i++)
            {
                var x = LootSystem.RollItem(a, Blade(), i, 3, 0.1f);
                var y = LootSystem.RollItem(b, Blade(), i, 3, 0.1f);
                Assert.AreEqual(x.Rarity, y.Rarity);
                Assert.AreEqual(x.Affixes.Count, y.Affixes.Count);
                for (int k = 0; k < x.Affixes.Count; k++)
                {
                    Assert.AreEqual(x.Affixes[k].Kind, y.Affixes[k].Kind);
                    Assert.AreEqual(x.Affixes[k].Value, y.Affixes[k].Value);
                }
            }
        }

        [Test]
        public void NullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LootSystem.RollItem(new Random(1), null, 1, 0, 0f));
        }
    }

    public sealed class InventoryTests
    {
        private static ItemInstance Item(int id, EquipSlot slotHint, params Affix[] affixes)
        {
            var item = new ItemInstance { InstanceId = id, DefinitionId = "def_" + slotHint, Rarity = ItemRarity.Rare };
            item.Affixes.AddRange(affixes);
            return item;
        }

        [Test]
        public void AddFindRemove_Work()
        {
            var inv = new Inventory();
            inv.Add(Item(1, EquipSlot.Weapon));
            Assert.IsNotNull(inv.Find(1));
            Assert.IsTrue(inv.Remove(1));
            Assert.IsNull(inv.Find(1));
            Assert.IsFalse(inv.Remove(1));
        }

        [Test]
        public void Equip_DisplacesPrevious()
        {
            var inv = new Inventory();
            inv.Add(Item(1, EquipSlot.Weapon));
            inv.Add(Item(2, EquipSlot.Weapon));
            inv.Equip(1, EquipSlot.Weapon, false);
            var displaced = inv.Equip(2, EquipSlot.Weapon, false);
            Assert.AreEqual(1, displaced.InstanceId);
            Assert.AreEqual(2, inv.GetEquipped(EquipSlot.Weapon, false).InstanceId);
        }

        [Test]
        public void TwoRings_EquipIndependently()
        {
            var inv = new Inventory();
            inv.Add(Item(1, EquipSlot.Ring));
            inv.Add(Item(2, EquipSlot.Ring));
            inv.Equip(1, EquipSlot.Ring, false);
            inv.Equip(2, EquipSlot.Ring, true);
            Assert.AreEqual(1, inv.GetEquipped(EquipSlot.Ring, false).InstanceId);
            Assert.AreEqual(2, inv.SecondRing.InstanceId);
        }

        [Test]
        public void AffixTotal_SumsEquippedOnly()
        {
            var inv = new Inventory();
            var a = Item(1, EquipSlot.Chest, new Affix { Kind = AffixKind.FlatArmour, Value = 10f });
            var b = Item(2, EquipSlot.Helm, new Affix { Kind = AffixKind.FlatArmour, Value = 5f });
            var c = Item(3, EquipSlot.Boots, new Affix { Kind = AffixKind.FlatArmour, Value = 99f });
            inv.Add(a); inv.Add(b); inv.Add(c);
            inv.Equip(1, EquipSlot.Chest, false);
            inv.Equip(2, EquipSlot.Helm, false);
            Assert.AreEqual(15f, inv.AffixTotal(AffixKind.FlatArmour), 0.001f);
        }

        [Test]
        public void RemovingEquippedItem_UnequipsIt()
        {
            var inv = new Inventory();
            inv.Add(Item(1, EquipSlot.Weapon));
            inv.Equip(1, EquipSlot.Weapon, false);
            inv.Remove(1);
            Assert.IsNull(inv.GetEquipped(EquipSlot.Weapon, false));
        }

        [Test]
        public void InstanceIds_NeverCollideAfterLoad()
        {
            var inv = new Inventory();
            inv.SetNextInstanceId(100);
            int id = inv.NextInstanceId();
            Assert.AreEqual(100, id);
            inv.Add(Item(150, EquipSlot.Weapon));
            Assert.AreEqual(151, inv.NextInstanceId());
        }
    }
}
