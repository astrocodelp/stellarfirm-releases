using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emberfall.Core
{
    /// <summary>Everything a save slot holds. Versioned; ids reference definitions, never asset GUIDs.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int Version = SaveSystem.CurrentVersion;
        public string SavedAtUtc;
        public int Level = 1;
        public int Xp;
        public int UnspentPoints;
        public Attributes Attributes = Attributes.Default();
        public float Ember = 100f;
        public int RunSeed;
        public int FloorIndex;
        public List<ItemInstance> Items = new List<ItemInstance>();
        public List<EquipEntry> Equipment = new List<EquipEntry>();
        public List<QuestState> Quests = new List<QuestState>();
        public List<string> WorldFlags = new List<string>();
        public int NextInstanceId = 1;
        public SettingsData Settings = new SettingsData();
    }

    [Serializable]
    public sealed class EquipEntry
    {
        public EquipSlot Slot;
        public bool SecondRing;
        public int InstanceId;
    }

    [Serializable]
    public sealed class SettingsData
    {
        public float TextScale = 1f;          // 1..2
        public bool ReducedShake = false;
        public bool ReducedFlashing = false;
        public bool Subtitles = true;
        public bool ToggleBlock = false;      // hold-to-block alternative
        public float MasterVolume = 1f;
    }

    /// <summary>
    /// Versioned JSON saves with atomic writes (temp file + replace) so a crash
    /// mid-write never destroys a slot. The local save is the truth; Steam Cloud
    /// sync comes later through IPlatformServices.
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 1;
        public const int SlotCount = 3;

        public static string SaveDirectory
        {
            get { return Path.Combine(Application.persistentDataPath, "saves"); }
        }

        public static string SlotPath(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(SaveDirectory, "slot" + slot + ".json");
        }

        public static void Save(int slot, SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Version = CurrentVersion;
            data.SavedAtUtc = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(data, true);
            WriteAtomic(SlotPath(slot), json);
        }

        /// <summary>Loads a slot, migrating older versions. Returns null when the slot is empty.</summary>
        public static SaveData Load(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            return FromJson(json);
        }

        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var probe = JsonUtility.FromJson<SaveData>(json);
            if (probe == null) return null;
            if (probe.Version < CurrentVersion)
            {
                json = SaveMigrations.Migrate(probe.Version, json);
                probe = JsonUtility.FromJson<SaveData>(json);
                if (probe != null) probe.Version = CurrentVersion;
            }
            Normalize(probe);
            return probe;
        }

        /// <summary>
        /// Repairs null blocks a hand-edited or foreign save might carry, so the
        /// rest of the game never null-checks save fields.
        /// </summary>
        private static void Normalize(SaveData data)
        {
            if (data == null) return;
            if (data.Settings == null) data.Settings = new SettingsData();
            if (data.Items == null) data.Items = new List<ItemInstance>();
            if (data.Equipment == null) data.Equipment = new List<EquipEntry>();
            if (data.Quests == null) data.Quests = new List<QuestState>();
            if (data.WorldFlags == null) data.WorldFlags = new List<string>();
            if (data.Attributes.Total() == 0) data.Attributes = Attributes.Default();
        }

        public static bool SlotExists(int slot) { return File.Exists(SlotPath(slot)); }

        /// <summary>Writes to a temp file then replaces, so a half-written save never lands.</summary>
        public static void WriteAtomic(string path, string contents)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, contents);
            if (File.Exists(path))
            {
                // File.Replace is atomic on the same volume; keep a one-shot backup.
                File.Replace(tmp, path, path + ".bak");
            }
            else
            {
                File.Move(tmp, path);
            }
        }
    }

    /// <summary>Upgrade steps for older save versions. Add a case per version bump.</summary>
    public static class SaveMigrations
    {
        public static string Migrate(int fromVersion, string json)
        {
            // Version 0 (pre-release dev saves) had no Settings block; JsonUtility
            // fills defaults, so the only step is stamping the new version.
            if (fromVersion < 1)
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return json;
                if (data.Attributes.Total() == 0) data.Attributes = Attributes.Default();
                data.Version = 1;
                return JsonUtility.ToJson(data, true);
            }
            return json;
        }
    }
}
