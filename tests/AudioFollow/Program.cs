using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LabApi.Features.Wrappers;
using Qlz;
using Qlz.Audio;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        checks++;
        Console.WriteLine("PASS " + text);
    }
    private static void PumpUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!done() && DateTime.UtcNow < deadline) { MEC.Timing.Step(); Thread.Sleep(5); }
        Check(done(), "async decode completed within deadline");
    }
    public static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "scp294-audio-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string fallback = Path.Combine(root, "jh.ogg");
        try
        {
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "AudioRobot"), "no external AudioRobot assembly loaded");
            Check(!File.Exists(Path.Combine(AppContext.BaseDirectory, "NVorbis.dll")), "decoder DLL is not copied beside test executable");
            EmbeddedVorbis.Initialize();
            Check(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "NVorbis"), "embedded decoder loaded");
            float[] samples = OggDecoder.Decode(fallback, CancellationToken.None);
            Check(samples.Length > 48000 && samples.All(s => !float.IsNaN(s) && Math.Abs(s) <= 1), "actual embedded jh.ogg decodes to valid 48k mono PCM");
            Check(samples.Any(s => Math.Abs(s) > 0.01), "actual jh.ogg contains audible non-silent samples");
            Console.WriteLine($"Decoded actual jh.ogg: {samples.Length / 48000f:0.00} seconds.");
            string overridePath = Path.Combine(root, "custom.ogg");
            using (Stream resource = typeof(Program).Assembly.GetManifestResourceStream("SCP294.Audio.jh.ogg")!)
            using (Stream output = File.Create(overridePath)) resource.CopyTo(output);
            float[] external = OggDecoder.Decode(overridePath, CancellationToken.None);
            Check(external.Length == samples.Length, "external configured OGG remains supported");
            var canceled = new CancellationTokenSource();
            canceled.Cancel();
            bool cancelSeen = false;
            try { OggDecoder.Decode(fallback, canceled.Token); } catch (OperationCanceledException) { cancelSeen = true; }
            Check(cancelSeen, "decode honors cancellation");
            bool missingSeen = false;
            try { OggDecoder.Decode(Path.Combine(root, "missing.ogg"), CancellationToken.None); } catch (FileNotFoundException) { missingSeen = true; }
            Check(missingSeen, "missing custom audio produces actionable failure");

            var other = SpeakerToy.Create(new Vector3(), false);
            other.ControllerId = 254;
            var player = new Player { Position = new Vector3 { x = 5 } };
            JiahaoAudio.Preload(fallback);
            PumpUntil(() => LabApi.Features.Console.Logger.Messages.Any(m => m.Contains("BGM 就绪")));
            var session = JiahaoAudio.Start(player, fallback, 5, 1)!;
            PumpUntil(() => SpeakerToy.List.Count > 1);
            var speaker = SpeakerToy.List.Last();
            Check(speaker.ControllerId == 253 && speaker.SpawnedBeforePlay, "unique controller ID and speaker spawned before PCM playback");
            Check(speaker.Loop && speaker.IsSpatial && speaker.MaxDistance == 5 && speaker.MinDistance == 1 && speaker.Volume == 1,
                "native LabAPI speaker uses loop and five-meter attenuation");
            Check(!speaker.IsStatic && speaker.SyncInterval == 0.1f, "speaker movement synchronizes to clients");
            Check(speaker.Samples!.Length == samples.Length, "native speaker receives actual decoded BGM samples");
            player.Position = new Vector3 { x = 20 };
            session.Follow(player);
            Check(speaker.Position.x == 20, "music speaker follows player movement");
            session.Stop();
            session.Stop();
            Check(speaker.Stopped && speaker.Destroyed && SpeakerToy.List.Count == 1, "stop is idempotent and preserves other plugins speaker");

            int created = SpeakerToy.Created;
            session = JiahaoAudio.Start(player, fallback, 5, 1)!;
            session.Stop();
            MEC.Timing.Step();
            Check(SpeakerToy.Created == created, "cancel before playback prevents late speaker creation");
            session = JiahaoAudio.Start(player, fallback, 5, 1)!;
            player.LifeId++;
            MEC.Timing.Step();
            Check(SpeakerToy.Created == created, "role change before coroutine starts cancels playback");
            session = JiahaoAudio.Start(player, fallback, 5, 1)!;
            MEC.Timing.Step();
            speaker = SpeakerToy.List.Last();
            JiahaoAudio.StopAll();
            Check(speaker.Destroyed && !other.Destroyed, "round reset cleans only owned sessions");
            session = JiahaoAudio.Start(player, Path.Combine(root, "missing.ogg"), 5, 1)!;
            PumpUntil(() => Hints.Failures > 0);
            Check(LabApi.Features.Console.Logger.Messages.Any(m => m.Contains("BGM 播放失败")), "playback failure logged and shown through HSM");
            session = JiahaoAudio.Start(player, fallback, 5, 1)!;
            MEC.Timing.Step();
            speaker = SpeakerToy.List.Last();
            JiahaoAudio.Shutdown();
            Check(speaker.Destroyed && !other.Destroyed, "unload clears owned BGM while preserving external audio");
            Console.WriteLine($"Passed {checks} embedded audio checks.");
        }
        finally { JiahaoAudio.Shutdown(); Directory.Delete(root, true); }
    }
}
namespace UnityEngine { public struct Vector3 { public float x; } }
namespace VoiceChat.Playbacks
{
    public class SpeakerToyPlaybackBase
    {
        public static readonly HashSet<SpeakerToyPlaybackBase> AllInstances = new();
        public byte ControllerId;
    }
}
namespace LabApi.Features.Wrappers
{
    public class Player { public Vector3 Position { get; set; } public int LifeId = 1; public bool IsAlive = true; public string Nickname = "Tester"; }
    public class SpeakerToy
    {
        public static readonly List<SpeakerToy> List = new();
        public static int Created;
        public byte ControllerId;
        public bool IsSpatial, IsStatic = true, Loop, Destroyed, Stopped, SpawnedBeforePlay;
        public float SyncInterval, Volume, MinDistance, MaxDistance;
        public Vector3 Position;
        public float[]? Samples;
        private bool spawned;
        public static SpeakerToy Create(Vector3 position, bool networkSpawn = true)
        {
            Created++;
            var speaker = new SpeakerToy { Position = position };
            List.Add(speaker);
            if (networkSpawn) speaker.Spawn();
            return speaker;
        }
        public void Spawn() => spawned = true;
        public void Play(float[] samples, bool queue = true, bool loop = false) { Samples = samples; Loop = loop; SpawnedBeforePlay = spawned; }
        public void Stop() => Stopped = true;
        public void Destroy() { Destroyed = true; List.Remove(this); }
    }
}
namespace LabApi.Features.Console
{
    public static class Logger
    {
        public static readonly List<string> Messages = new();
        public static void Warn(string text) => Messages.Add(text);
        public static void Info(string text) => Messages.Add(text);
    }
}
namespace Qlz
{
    internal static class Hints
    {
        public static int Failures;
        public static void Show(Player player, string id, string text, float y, int fontSize, float lifetime) => Failures++;
    }
}
namespace MEC
{
    public struct CoroutineHandle { internal int Id; public bool IsValid => Id > 0; }
    public static class Timing
    {
        public const float WaitForOneFrame = 0;
        private static int nextId;
        private static readonly Dictionary<int, IEnumerator<float>> Running = new();
        public static CoroutineHandle RunCoroutine(IEnumerator<float> routine)
        {
            int id = ++nextId;
            Running.Add(id, routine);
            return new CoroutineHandle { Id = id };
        }
        public static void KillCoroutines(CoroutineHandle handle) => Running.Remove(handle.Id);
        public static void Step()
        {
            foreach (var entry in Running.ToArray())
                if (Running.ContainsKey(entry.Key) && !entry.Value.MoveNext()) Running.Remove(entry.Key);
        }
    }
}
