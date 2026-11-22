using System;
using System.Collections.Generic;

namespace Emberfall.Core
{
    public enum RoomKind { Entry, Combat, Treasure, Shrine, Elite, BossGate }

    [Serializable]
    public sealed class RoomNode
    {
        public int Id;
        public RoomKind Kind;
        public List<int> Connections = new List<int>();
        public int EncounterSeed;
    }

    [Serializable]
    public sealed class DungeonFloor
    {
        public int FloorIndex;
        public int Seed;
        public List<RoomNode> Rooms = new List<RoomNode>();
        public int EntryRoomId;
        public int BossRoomId;
    }

    /// <summary>
    /// Deterministic seeded floor generator: entry -> 4..8 rooms -> boss gate,
    /// laid out as a main spine with occasional branches. Same (runSeed, floor)
    /// always yields the same floor — daily runs and save-resume rely on this.
    /// </summary>
    public static class DungeonGenerator
    {
        public static DungeonFloor Generate(int runSeed, int floorIndex)
        {
            if (floorIndex < 0) throw new ArgumentOutOfRangeException(nameof(floorIndex));
            // Combine seeds deterministically (unchecked mix, no UnityEngine).
            int seed = unchecked(runSeed * 48611 + floorIndex * 953 + 17);
            var rng = new Random(seed);

            var floor = new DungeonFloor { FloorIndex = floorIndex, Seed = seed };
            int middleCount = 4 + rng.Next(5); // 4..8

            var entry = NewRoom(floor, 0, RoomKind.Entry, rng);
            floor.EntryRoomId = entry.Id;

            int previous = entry.Id;
            for (int i = 0; i < middleCount; i++)
            {
                RoomKind kind = RollMiddleKind(rng);
                var room = NewRoom(floor, floor.Rooms.Count, kind, rng);
                Connect(floor, previous, room.Id);
                previous = room.Id;

                // 30% chance of a dead-end side room (treasure or shrine)
                if (rng.NextDouble() < 0.3)
                {
                    RoomKind side = rng.NextDouble() < 0.6 ? RoomKind.Treasure : RoomKind.Shrine;
                    var branch = NewRoom(floor, floor.Rooms.Count, side, rng);
                    Connect(floor, room.Id, branch.Id);
                }
            }

            var boss = NewRoom(floor, floor.Rooms.Count, RoomKind.BossGate, rng);
            floor.BossRoomId = boss.Id;
            Connect(floor, previous, boss.Id);
            return floor;
        }

        private static RoomKind RollMiddleKind(Random rng)
        {
            double roll = rng.NextDouble();
            if (roll < 0.60) return RoomKind.Combat;
            if (roll < 0.75) return RoomKind.Treasure;
            if (roll < 0.90) return RoomKind.Shrine;
            return RoomKind.Elite;
        }

        private static RoomNode NewRoom(DungeonFloor floor, int id, RoomKind kind, Random rng)
        {
            var room = new RoomNode { Id = id, Kind = kind, EncounterSeed = rng.Next() };
            floor.Rooms.Add(room);
            return room;
        }

        private static void Connect(DungeonFloor floor, int a, int b)
        {
            floor.Rooms[a].Connections.Add(b);
            floor.Rooms[b].Connections.Add(a);
        }

        /// <summary>True when every room is reachable from the entry (connectivity check).</summary>
        public static bool IsFullyConnected(DungeonFloor floor)
        {
            if (floor == null || floor.Rooms.Count == 0) return false;
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(floor.EntryRoomId);
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                if (!seen.Add(id)) continue;
                var room = floor.Rooms[id];
                for (int i = 0; i < room.Connections.Count; i++)
                    if (!seen.Contains(room.Connections[i])) stack.Push(room.Connections[i]);
            }
            return seen.Count == floor.Rooms.Count;
        }
    }
}
