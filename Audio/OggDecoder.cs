using System;
using System.IO;
using System.Threading;
using NVorbis;

namespace Qlz.Audio;

internal static class OggDecoder
{
    internal const int SampleRate = 48000;
    private const int MaxSeconds = 600;

    // Only file/resource reading and managed decoding run on a worker thread.
    internal static float[] Decode(string path, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("嘉豪音乐需使用 OGG Vorbis 格式。");
        Stream stream;
        if (File.Exists(path)) stream = File.OpenRead(path);
        else if (Path.GetFileName(path).Equals("jh.ogg", StringComparison.OrdinalIgnoreCase))
            stream = typeof(OggDecoder).Assembly.GetManifestResourceStream("SCP294.Audio.jh.ogg")
                ?? throw new FileNotFoundException("内嵌嘉豪音乐缺失。", path);
        else throw new FileNotFoundException("找不到嘉豪音乐。", path);
        using (stream)
        using (var reader = new VorbisReader(stream, false))
        {
            if (reader.SampleRate <= 0 || reader.Channels < 1 || reader.Channels > 8)
                throw new InvalidDataException("OGG 音频参数无效。");
            long count = reader.TotalSamples;
            long limit = Math.Min((long)reader.SampleRate * MaxSeconds, (long)SampleRate * MaxSeconds);
            if (count < 1 || count > limit) throw new InvalidDataException("嘉豪音乐为空或超过十分钟/解码容量限制。");
            var mono = new float[(int)count];
            var input = new float[4096 * reader.Channels];
            int frames = 0;
            int read;
            while ((read = reader.ReadSamples(input, 0, input.Length)) > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                if (read % reader.Channels != 0 || frames + read / reader.Channels > mono.Length)
                    throw new InvalidDataException("OGG 样本长度不匹配。");
                for (int i = 0; i < read; i += reader.Channels)
                {
                    double sum = 0;
                    for (int channel = 0; channel < reader.Channels; channel++)
                    {
                        float sample = input[i + channel];
                        if (!float.IsNaN(sample) && !float.IsInfinity(sample)) sum += sample;
                    }
                    mono[frames++] = (float)Math.Max(-1d, Math.Min(1d, sum / reader.Channels));
                }
            }
            if (frames == 0) throw new InvalidDataException("OGG 没有可播放样本。");
            Array.Resize(ref mono, frames);
            if (reader.SampleRate == SampleRate) return mono;
            int outputCount = Math.Max(1, (int)Math.Round((double)frames * SampleRate / reader.SampleRate));
            if (outputCount > SampleRate * MaxSeconds) throw new InvalidDataException("音乐长度超过十分钟。");
            var result = new float[outputCount];
            double step = (double)reader.SampleRate / SampleRate;
            for (int i = 0; i < result.Length; i++)
            {
                if ((i & 0x3FFF) == 0) cancellation.ThrowIfCancellationRequested();
                double source = i * step;
                int left = Math.Min((int)source, frames - 1);
                int right = Math.Min(left + 1, frames - 1);
                result[i] = mono[left] + (mono[right] - mono[left]) * (float)(source - left);
            }
            return result;
        }
    }
}
