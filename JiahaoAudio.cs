using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LabApi.Features.Wrappers;
using MEC;
using Qlz.Audio;
using VoiceChat.Playbacks;
using Logger = LabApi.Features.Console.Logger;

namespace Qlz;

// Owns only SCP294 sessions. Decoding is asynchronous; speaker creation, playback,
// following and cleanup all happen on Unity's main thread through native LabAPI.
internal sealed class JiahaoAudio
{
    private static readonly Dictionary<string, Task<float[]>> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<JiahaoAudio> Sessions = new();
    private static CancellationTokenSource? lifetime;
    private static CoroutineHandle preloadMonitor;
    private SpeakerToy? speaker;
    private CoroutineHandle pending;
    private bool cancelled;

    internal static void Preload(string path)
    {
        try
        {
            if (preloadMonitor.IsValid) Timing.KillCoroutines(preloadMonitor);
            preloadMonitor = Timing.RunCoroutine(ReportPreload(GetSamples(path), path));
        }
        catch (Exception ex) { Logger.Warn("[SCP-294?] BGM preload: " + ex.GetBaseException().Message); }
    }

    private static Task<float[]> GetSamples(string path)
    {
        EmbeddedVorbis.Initialize();
        lifetime ??= new CancellationTokenSource();
        string fullPath = Path.GetFullPath(path);
        if (Cache.TryGetValue(fullPath, out Task<float[]>? cached) && cached != null && !cached.IsFaulted && !cached.IsCanceled) return cached;
        if (Cache.Count >= 4) Cache.Clear();
        CancellationToken token = lifetime.Token;
        Task<float[]> task = Task.Run(() => OggDecoder.Decode(fullPath, token), token);
        // Observe failures even if a player leaves before the pending session completes.
        task.ContinueWith(failed => { _ = failed.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        Cache[fullPath] = task;
        return task;
    }

    private static IEnumerator<float> ReportPreload(Task<float[]> task, string path)
    {
        while (!task.IsCompleted) yield return Timing.WaitForOneFrame;
        if (task.IsFaulted) Logger.Warn("[SCP-294?] BGM decode: " + task.Exception?.GetBaseException().Message);
        else if (!task.IsCanceled) Logger.Info($"[SCP-294?] 内嵌 BGM 就绪：{Path.GetFileName(path)}，{task.Result.Length / 48000f:0.0} 秒。");
    }

    internal static JiahaoAudio? Start(Player player, string path, float radius, float volume)
    {
        try
        {
            Task<float[]> task = GetSamples(path);
            var session = new JiahaoAudio();
            Sessions.Add(session);
            session.pending = Timing.RunCoroutine(session.WaitForPlayback(task, player, player.LifeId, radius, volume));
            return session;
        }
        catch (Exception ex) { ReportFailure(player, ex); return null; }
    }

    private IEnumerator<float> WaitForPlayback(Task<float[]> task, Player player, int lifeId, float radius, float volume)
    {
        while (!task.IsCompleted && !cancelled) yield return Timing.WaitForOneFrame;
        pending = default;
        if (cancelled) yield break;
        if (!player.IsAlive || player.LifeId != lifeId || task.IsCanceled) { Stop(); yield break; }
        try
        {
            float[] samples = task.GetAwaiter().GetResult();
            byte id = AllocateControllerId();
            speaker = SpeakerToy.Create(player.Position, networkSpawn: false);
            speaker.ControllerId = id;
            speaker.IsStatic = false;
            speaker.SyncInterval = 0.1f;
            speaker.IsSpatial = true;
            speaker.Volume = float.IsNaN(volume) || float.IsInfinity(volume) ? 1f : Math.Max(0f, Math.Min(2f, volume));
            speaker.MaxDistance = float.IsNaN(radius) || float.IsInfinity(radius) ? 5f : Math.Max(0.1f, radius);
            speaker.MinDistance = Math.Min(1f, speaker.MaxDistance);
            speaker.Spawn();
            speaker.Play(samples, queue: false, loop: true);
            Logger.Info($"[SCP-294?] 嘉豪 BGM 播放：{player.Nickname}，音源 {id}，半径 {speaker.MaxDistance:0.0} 米。");
        }
        catch (Exception ex) { ReportFailure(player, ex); Stop(); }
    }

    private static byte AllocateControllerId()
    {
        // Account for other plugins' native toys AND playback controllers.
        var occupied = new HashSet<byte>(SpeakerToy.List.Select(s => s.ControllerId));
        foreach (SpeakerToyPlaybackBase playback in SpeakerToyPlaybackBase.AllInstances) occupied.Add(playback.ControllerId);
        for (int id = 254; id >= 0; id--) if (!occupied.Contains((byte)id)) return (byte)id;
        throw new InvalidOperationException("没有空闲扬声器 ControllerId。");
    }

    private static void ReportFailure(Player player, Exception ex)
    {
        Logger.Warn("[SCP-294?] 嘉豪 BGM 播放失败：" + ex.GetBaseException().Message);
        Hints.Show(player, "audio-error", "嘉豪 BGM 播放失败，请查看服务器日志。", 430f, 21, 5f);
    }

    internal void Follow(Player player)
    {
        if (cancelled || speaker == null) return;
        try { speaker.Position = player.Position; }
        catch (Exception ex) { ReportFailure(player, ex); Stop(); }
    }

    internal void Stop()
    {
        if (cancelled) return;
        cancelled = true;
        Sessions.Remove(this);
        if (pending.IsValid) Timing.KillCoroutines(pending);
        pending = default;
        SpeakerToy? owned = speaker;
        speaker = null;
        if (owned == null) return;
        try { owned.Stop(); }
        catch (Exception ex) { Logger.Warn("[SCP-294?] BGM stop: " + ex.GetBaseException().Message); }
        finally
        {
            try { owned.Destroy(); }
            catch (Exception ex) { Logger.Warn("[SCP-294?] BGM destroy: " + ex.GetBaseException().Message); }
        }
    }

    internal static void StopAll()
    {
        foreach (JiahaoAudio session in Sessions.ToArray()) session.Stop();
    }

    internal static void Shutdown()
    {
        StopAll();
        if (preloadMonitor.IsValid) Timing.KillCoroutines(preloadMonitor);
        preloadMonitor = default;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        Cache.Clear();
        EmbeddedVorbis.Shutdown();
    }
}
