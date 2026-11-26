using KeyboardMixer.Core.Processes;
using NAudio.CoreAudioApi;

namespace KeyboardMixer.Core.Audio;

/// <summary>An application with one or more audio sessions.</summary>
public sealed record AudioApp(string ExeName, string DisplayName, string? ExePath, int VolumePercent, int SessionCount);

/// <summary>Result of a volume step or mute toggle. <see cref="VolumePercent"/> is null when the app has no audio sessions.</summary>
public sealed record VolumeChange(string ExeName, string DisplayName, string? ExePath, int? VolumePercent, bool IsMuted = false);

/// <summary>
/// Reads and changes per-application volume through the Windows Core Audio session APIs.
/// Sessions are gathered from every active playback device, not just the default one, and are
/// grouped by executable name so multi-process apps (browsers, Discord, Spotify) act as one.
///
/// Not thread-safe: create and use it from a single MTA thread (see AudioWorker in the app).
/// </summary>
public sealed class AudioSessionService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    /// <summary>Apps that currently have an audio session, sorted by display name.</summary>
    public IReadOnlyList<AudioApp> GetApps()
    {
        using var snapshot = TakeSnapshot();
        return snapshot.Sessions
            .GroupBy(s => s.ExeName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var path = g.Select(s => s.ExePath).FirstOrDefault(p => p is not null);
                return new AudioApp(
                    first.ExeName,
                    ProcessInfo.GetDisplayName(path, first.ExeName),
                    path,
                    VolumeMath.ToPercent(first.Control.SimpleAudioVolume.Volume),
                    g.Count());
            })
            .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Steps the volume of every session belonging to <paramref name="exeName"/>. All sessions are
    /// set to the same new level, computed from the first session's current level. Like the
    /// Windows volume keys, a step also unmutes the app.
    /// </summary>
    public VolumeChange StepVolume(string exeName, string? knownExePath, int stepPercent, bool up) =>
        Apply(exeName, knownExePath, matches =>
        {
            float next = VolumeMath.Step(matches[0].Control.SimpleAudioVolume.Volume, stepPercent, up);
            ForEachSession(matches, v =>
            {
                v.Volume = next;
                if (v.Mute) v.Mute = false;
            });
            return (VolumeMath.ToPercent(next), false);
        });

    /// <summary>
    /// Toggles mute for every session belonging to <paramref name="exeName"/>. The new state is
    /// the opposite of the first session's, and all sessions are set to it.
    /// </summary>
    public VolumeChange ToggleMute(string exeName, string? knownExePath) =>
        Apply(exeName, knownExePath, matches =>
        {
            var first = matches[0].Control.SimpleAudioVolume;
            bool mute = !first.Mute;
            ForEachSession(matches, v => v.Mute = mute);
            return (VolumeMath.ToPercent(first.Volume), mute);
        });

    private VolumeChange Apply(string exeName, string? knownExePath, Func<List<SessionEntry>, (int Volume, bool Muted)> change)
    {
        using var snapshot = TakeSnapshot();
        var matches = snapshot.Sessions.Where(s => ExeName.Matches(s.ExeName, exeName)).ToList();

        var path = matches.Select(s => s.ExePath).FirstOrDefault(p => p is not null) ?? knownExePath;
        var displayName = ProcessInfo.GetDisplayName(path, exeName);

        if (matches.Count == 0)
            return new VolumeChange(exeName, displayName, path, null);

        var (volume, muted) = change(matches);
        return new VolumeChange(exeName, displayName, path, volume, muted);
    }

    private static void ForEachSession(List<SessionEntry> sessions, Action<SimpleAudioVolume> set)
    {
        foreach (var session in sessions)
        {
            try
            {
                set(session.Control.SimpleAudioVolume);
            }
            catch (Exception)
            {
                // The session can expire between enumeration and the set call; ignore it.
            }
        }
    }

    public void Dispose() => _enumerator.Dispose();

    private sealed record SessionEntry(AudioSessionControl Control, string ExeName, string? ExePath);

    /// <summary>Owns the COM objects for one enumeration pass and releases them on dispose.</summary>
    private sealed class Snapshot : IDisposable
    {
        public List<MMDevice> Devices { get; } = [];
        public List<SessionEntry> Sessions { get; } = [];
        public List<AudioSessionControl> Skipped { get; } = [];

        public void Dispose()
        {
            foreach (var s in Sessions) s.Control.Dispose();
            foreach (var s in Skipped) s.Dispose();
            foreach (var d in Devices) d.Dispose();
        }
    }

    private Snapshot TakeSnapshot()
    {
        var snapshot = new Snapshot();
        try
        {
            foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                snapshot.Devices.Add(device);
                SessionCollection sessions;
                try
                {
                    sessions = device.AudioSessionManager.Sessions;
                }
                catch (Exception)
                {
                    continue; // device went away mid-enumeration
                }

                for (int i = 0; i < sessions.Count; i++)
                {
                    var control = sessions[i];
                    if (!TryDescribe(control, out var exeName, out var exePath))
                    {
                        snapshot.Skipped.Add(control);
                        continue;
                    }
                    snapshot.Sessions.Add(new SessionEntry(control, exeName, exePath));
                }
            }
        }
        catch
        {
            snapshot.Dispose();
            throw;
        }
        return snapshot;
    }

    private static bool TryDescribe(AudioSessionControl control, out string exeName, out string? exePath)
    {
        exeName = "";
        exePath = null;
        try
        {
            if (control.IsSystemSoundsSession || control.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateExpired)
                return false;

            exePath = ProcessInfo.GetExePath((int)control.GetProcessID);
            if (exePath is null) return false;
            exeName = Path.GetFileName(exePath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
