using System.Threading;
using System.Threading.Tasks;

namespace Emberfall.Platform
{
    /// <summary>
    /// The store seam. Gameplay only ever talks to this interface; Steam,
    /// itch.io, GOG, Epic implementations live behind their own build defines.
    /// The local save is always the truth; cloud is a mirror.
    /// </summary>
    public interface IPlatformServices
    {
        string PlatformName { get; }
        bool IsAvailable { get; }
        string PlayerDisplayName { get; }

        void UnlockAchievement(string achievementId);
        void SetStat(string statId, float value);

        /// <summary>Uploads a save blob to the platform cloud. No-op where unsupported.</summary>
        Task CloudWriteAsync(string fileName, byte[] data, CancellationToken ct);
        /// <summary>Downloads a save blob, or null when absent/unsupported.</summary>
        Task<byte[]> CloudReadAsync(string fileName, CancellationToken ct);

        void ShowOverlay();
    }

    /// <summary>Default implementation: everything is a safe no-op. Used in Editor and non-store builds.</summary>
    public sealed class NullPlatformServices : IPlatformServices
    {
        public string PlatformName { get { return "None"; } }
        public bool IsAvailable { get { return false; } }
        public string PlayerDisplayName { get { return "Emberbearer"; } }

        public void UnlockAchievement(string achievementId) { }
        public void SetStat(string statId, float value) { }

        public Task CloudWriteAsync(string fileName, byte[] data, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<byte[]> CloudReadAsync(string fileName, CancellationToken ct)
        {
            return Task.FromResult<byte[]>(null);
        }

        public void ShowOverlay() { }
    }

    /// <summary>Composition root picks the implementation once at boot.</summary>
    public static class PlatformServices
    {
        private static IPlatformServices _current = new NullPlatformServices();

        public static IPlatformServices Current { get { return _current; } }

        public static void Set(IPlatformServices services)
        {
            _current = services ?? new NullPlatformServices();
        }
    }
}
