using System;
using System.Collections.Generic;

namespace Emberfall.Core
{
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    public enum EquipSlot { Weapon, Helm, Chest, Gloves, Boots, Ring, Amulet, Consumable }

    public enum AffixKind
    {
        FlatHealth, FlatStamina, CritChance, StaminaRegen,
        Lifesteal, FlatArmour, EmberCapacity, FocusCostReduction
    }

    [Serializable]
    public struct Affix
    {
        public AffixKind Kind;
        public float Value;
    }

    /// <summary>Immutable item definition (mirrors the ScriptableObject; id-keyed for saves).</summary>
    public sealed class ItemDefinition
    {
        public string Id;
        public string DisplayName;
        public EquipSlot Slot;
        public float BaseDamage;      // weapons
        public int BaseArmour;        // armour pieces
        public char StrScaling = 'E';
        public char DexScaling = 'E';
        public float AttackSpeed = 1f;
    }

    /// <summary>A rolled item instance: definition id + rarity + affixes. Serializable into saves.</summary>
    [Serializable]
    public sealed class ItemInstance
    {
        public int InstanceId;
        public string DefinitionId;
        public ItemRarity Rarity;
        public List<Affix> Affixes = new List<Affix>();
        public int StackCount = 1;
    }

    public static class RarityRules
    {
        public static int AffixCount(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 0;
                case ItemRarity.Uncommon: return 1;
                case ItemRarity.Rare: return 2;
                case ItemRarity.Epic: return 3;
                case ItemRarity.Legendary: return 4;
                default: return 0;
            }
        }

        /// <summary>Rarity word shown in UI — meaning never rests on colour alone.</summary>
        public static string Label(ItemRarity rarity)
        {
            return rarity.ToString();
        }
    }

    /// <summary>
    /// Seeded loot rolling. Deterministic for a given Random sequence, so
    /// room loot reproduces exactly from (runSeed, floor, room).
    /// </summary>
    public static class LootSystem
    {
        private static readonly AffixKind[] AllAffixes =
        {
            AffixKind.FlatHealth, AffixKind.FlatStamina, AffixKind.CritChance,
            AffixKind.StaminaRegen, AffixKind.Lifesteal, AffixKind.FlatArmour,
            AffixKind.EmberCapacity, AffixKind.FocusCostReduction
        };

        /// <summary>Rarity weights: 55/25/12/6/2 percent, shifted by luck.</summary>
        public static ItemRarity RollRarity(Random rng, float luckBonus)
        {
            // luckBonus 0..1 shifts weight from common toward higher tiers
            float shift = luckBonus < 0f ? 0f : (luckBonus > 1f ? 1f : luckBonus);
            float w0 = 55f - 30f * shift;
            float w1 = 25f + 10f * shift;
            float w2 = 12f + 10f * shift;
            float w3 = 6f + 7f * shift;
            float w4 = 2f + 3f * shift;
            float total = w0 + w1 + w2 + w3 + w4;
            double roll = rng.NextDouble() * total;
            if (roll < w0) return ItemRarity.Common;
            roll -= w0;
            if (roll < w1) return ItemRarity.Uncommon;
            roll -= w1;
            if (roll < w2) return ItemRarity.Rare;
            roll -= w2;
            if (roll < w3) return ItemRarity.Epic;
            return ItemRarity.Legendary;
        }

        public static float RollAffixValue(Random rng, AffixKind kind, int floorIndex)
        {
            float depth = 1f + 0.1f * (floorIndex < 0 ? 0 : floorIndex);
            float t = (float)rng.NextDouble();
            switch (kind)
            {
                case AffixKind.FlatHealth: return (float)Math.Round((10 + 20 * t) * depth);
                case AffixKind.FlatStamina: return (float)Math.Round((5 + 10 * t) * depth);
                case AffixKind.CritChance: return (float)Math.Round((0.02 + 0.04 * t) * 100) / 100f;
                case AffixKind.StaminaRegen: return (float)Math.Round((1 + 3 * t) * depth * 10) / 10f;
                case AffixKind.Lifesteal: return (float)Math.Round((0.01 + 0.03 * t) * 100) / 100f;
                case AffixKind.FlatArmour: return (float)Math.Round((5 + 15 * t) * depth);
                case AffixKind.EmberCapacity: return (float)Math.Round((10 + 20 * t) * depth);
                case AffixKind.FocusCostReduction: return (float)Math.Round((0.03 + 0.07 * t) * 100) / 100f;
                default: return 1f;
            }
        }

        /// <summary>Rolls a full item instance from a definition. Caller owns instance ids.</summary>
        public static ItemInstance RollItem(Random rng, ItemDefinition def, int instanceId, int floorIndex, float luckBonus)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var item = new ItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = def.Id,
                Rarity = RollRarity(rng, luckBonus)
            };
            int count = RarityRules.AffixCount(item.Rarity);
            // pick distinct affix kinds via partial Fisher-Yates on a small copy
            var pool = (AffixKind[])AllAffixes.Clone();
            for (int i = 0; i < count; i++)
            {
                int j = i + rng.Next(pool.Length - i);
                AffixKind tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
                item.Affixes.Add(new Affix
                {
                    Kind = pool[i],
                    Value = RollAffixValue(rng, pool[i], floorIndex)
                });
            }
            return item;
        }
    }

    /// <summary>
    /// Inventory + equipment as plain state. Equipment: one per slot except rings (two).
    /// Pure logic; UI reads it, never mutates it directly.
    /// </summary>
    public sealed class Inventory
    {
        private readonly List<ItemInstance> _items = new List<ItemInstance>();
        private readonly Dictionary<EquipSlot, ItemInstance> _equipped = new Dictionary<EquipSlot, ItemInstance>();
        private ItemInstance _secondRing;
        private int _nextInstanceId = 1;

        public IReadOnlyList<ItemInstance> Items { get { return _items; } }
        public IReadOnlyDictionary<EquipSlot, ItemInstance> Equipped { get { return _equipped; } }
        public ItemInstance SecondRing { get { return _secondRing; } }

        public int NextInstanceId() { return _nextInstanceId++; }

        /// <summary>Restores the id counter after loading a save so ids never collide.</summary>
        public void SetNextInstanceId(int value)
        {
            if (value > _nextInstanceId) _nextInstanceId = value;
        }

        public void Add(ItemInstance item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _items.Add(item);
            SetNextInstanceId(item.InstanceId + 1);
        }

        public bool Remove(int instanceId)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].InstanceId == instanceId)
                {
                    UnequipIfEquipped(instanceId);
                    _items.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public ItemInstance Find(int instanceId)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].InstanceId == instanceId) return _items[i];
            return null;
        }

        /// <summary>Equips an owned item into its slot. Returns the displaced item (or null).</summary>
        public ItemInstance Equip(int instanceId, EquipSlot slot, bool secondRingSlot)
        {
            var item = Find(instanceId);
            if (item == null) return null;
            if (slot == EquipSlot.Consumable || slot == EquipSlot.Weapon && item == null) return null;

            ItemInstance displaced;
            if (slot == EquipSlot.Ring && secondRingSlot)
            {
                displaced = _secondRing;
                if (displaced != null && displaced.InstanceId == instanceId) return null;
                _secondRing = item;
                return displaced;
            }
            _equipped.TryGetValue(slot, out displaced);
            if (displaced != null && displaced.InstanceId == instanceId) return null;
            if (displaced != null && displaced == _secondRing) _secondRing = null;
            _equipped[slot] = item;
            return displaced;
        }

        public void Unequip(EquipSlot slot, bool secondRingSlot)
        {
            if (slot == EquipSlot.Ring && secondRingSlot) { _secondRing = null; return; }
            var item = GetEquipped(slot, false);
            if (item != null && item == _secondRing) _secondRing = null;
            _equipped.Remove(slot);
        }

        public ItemInstance GetEquipped(EquipSlot slot, bool secondRingSlot)
        {
            if (slot == EquipSlot.Ring && secondRingSlot) return _secondRing;
            ItemInstance item;
            _equipped.TryGetValue(slot, out item);
            return item;
        }

        /// <summary>Sums one affix kind across all equipped items.</summary>
        public float AffixTotal(AffixKind kind)
        {
            float total = 0f;
            foreach (var kv in _equipped) total += SumAffix(kv.Value, kind);
            if (_secondRing != null) total += SumAffix(_secondRing, kind);
            return total;
        }

        private static float SumAffix(ItemInstance item, AffixKind kind)
        {
            if (item == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < item.Affixes.Count; i++)
                if (item.Affixes[i].Kind == kind) sum += item.Affixes[i].Value;
            return sum;
        }

        private void UnequipIfEquipped(int instanceId)
        {
            if (_secondRing != null && _secondRing.InstanceId == instanceId) _secondRing = null;
            EquipSlot toRemove = EquipSlot.Consumable;
            bool found = false;
            foreach (var kv in _equipped)
            {
                if (kv.Value.InstanceId == instanceId) { toRemove = kv.Key; found = true; break; }
            }
            if (found) _equipped.Remove(toRemove);
        }
    }
}
