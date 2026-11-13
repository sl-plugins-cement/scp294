using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.PlayableScps.Scp079.Cameras;
using UnityEngine;
using VoiceChat.Networking;
using VoiceChat.Playbacks;
using Logger = LabApi.Features.Console.Logger;

namespace Qlz;

// The embedded packets are already encoded for the native decoder: no runtime PCM
// expansion or re-encoding at the native transmitter's fixed 120 kb/s bitrate.
internal sealed class CabinetAudio : IDisposable
{
    private const double FrameSeconds = 0.06;
    private static readonly Lazy<byte[][]> Clip = new(LoadClip);
    private readonly SpeakerToy speaker;
    private readonly List<Player> listeners = new();
    private CoroutineHandle playback;
    private bool disposed;
    internal long PacketsSent { get; private set; }
    internal long PayloadBytesSent { get; private set; }
    internal int ListenerCount => listeners.Count;

    private CabinetAudio(Vector3 position, float radius, float volume)
    {
        var occupied = new HashSet<byte>(SpeakerToy.List.Select(s => s.ControllerId));
        foreach (SpeakerToyPlaybackBase other in SpeakerToyPlaybackBase.AllInstances) occupied.Add(other.ControllerId);
        int id = 254;
        while (id >= 0 && occupied.Contains((byte)id)) id--;
        if (id < 0) throw new InvalidOperationException("No free SpeakerToy controller ID.");
        speaker = SpeakerToy.Create(position, networkSpawn: false);
        try
        {
            speaker.GameObject.name = "SCP294-CabinetSpeaker";
            speaker.ControllerId = (byte)id;
            speaker.IsStatic = true;
            speaker.IsSpatial = true;
            // Keep full volume at the front panel before applying distance falloff.
            speaker.MinDistance = Mathf.Min(2.5f, radius);
            speaker.MaxDistance = radius;
            speaker.Volume = volume;
            speaker.Spawn();
            playback = Timing.RunCoroutine(Transmit());
        }
        catch { speaker.Destroy(); throw; }
    }

    internal static CabinetAudio? Start(Vector3 position, float radius, float volume)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius)) radius = 10f;
        if (float.IsNaN(volume) || float.IsInfinity(volume)) volume = 1.2f;
        radius = Mathf.Clamp(radius, 1f, 100f);
        volume = Mathf.Clamp(volume, 0f, 2f);
        if (volume == 0f) return null;
        try
        {
            _ = Clip.Value;
            return new CabinetAudio(position, radius, volume);
        }
        catch (Exception ex)
        {
            Logger.Warn("[SCP-294?] Cabinet music: " + ex.GetBaseException().Message);
            return null;
        }
    }

    private static byte[][] LoadClip()
    {
        using Stream stream = typeof(CabinetAudio).Assembly.GetManifestResourceStream("SCP294.Audio.cabinet.opuspack")
            ?? throw new FileNotFoundException("Embedded cabinet music is missing.");
        using var reader = new BinaryReader(stream);
        if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "R294OP01") throw new InvalidDataException("Invalid cabinet audio header.");
        int count = reader.ReadInt32();
        if (count < 1 || count > 15000) throw new InvalidDataException("Invalid cabinet audio duration.");
        var packets = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            int length = reader.ReadUInt16();
            if (length != 120) throw new InvalidDataException("Expected 16 kb/s cabinet audio.");
            packets[i] = reader.ReadBytes(length);
            if (packets[i].Length != length) throw new EndOfStreamException("Truncated cabinet audio.");
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected cabinet audio data.");
        return packets;
    }

    internal bool CanHear(Player player)
    {
        if (disposed || speaker.IsDestroyed || speaker.Volume <= 0f || player.IsHost || player.IsDummy ||
            player.ConnectionToClient == null || !player.IsPlayer || !player.IsReady) return false;
        // The spectator camera follows its selected actor, not the spectator's server transform.
        Player? viewpoint = player.IsAlive ? player : player.CurrentlySpectating;
        if (viewpoint?.IsAlive != true) return false;
        Vector3 camera = viewpoint.RoleBase is ICameraController controller ? controller.CameraPosition : viewpoint.Camera.position;
        // SCP-079's role camera pose is updated client-side; use its server-synced selected camera.
        if (viewpoint.RoleBase is Scp079Role scp079)
        {
            if (!scp079.SubroutineModule.TryGetSubroutine<Scp079CurrentCameraSync>(out var sync) || !sync.TryGetCurrentCamera(out var selected)) return false;
            camera = selected.CameraPosition;
        }
        return (camera - speaker.Position).sqrMagnitude < speaker.MaxDistance * speaker.MaxDistance;
    }

    private IEnumerator<float> Transmit()
    {
        byte[][] packets = Clip.Value;
        double started = NetworkTime.time;
        long nextFrame = 0;
        while (!disposed && !speaker.IsDestroyed)
        {
            // Preserve the shared song position with no listeners. Bound catch-up after a
            // server stall so old music never bursts across the network or fills client buffers.
            long currentFrame = Math.Max(0, (long)((NetworkTime.time - started) / FrameSeconds));
            nextFrame = Math.Max(nextFrame, currentFrame - 1);
            listeners.Clear();
            foreach (Player player in Player.List)
                if (CanHear(player)) listeners.Add(player);
            while (nextFrame <= currentFrame)
            {
                byte[] data = packets[(int)(nextFrame++ % packets.Length)];
                var message = new AudioMessage(speaker.ControllerId, data, data.Length);
                foreach (Player player in listeners)
                {
                    player.Connection.Send(message);
                    PacketsSent++;
                    PayloadBytesSent += data.Length;
                }
            }
            yield return Timing.WaitForOneFrame;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (playback.IsValid) Timing.KillCoroutines(playback);
        listeners.Clear();
        if (!speaker.IsDestroyed) speaker.Destroy();
    }
}
