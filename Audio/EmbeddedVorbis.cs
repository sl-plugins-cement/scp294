using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Qlz.Audio;

internal static class EmbeddedVorbis
{
    private static readonly object Sync = new();
    private static Assembly? decoder;
    private static bool initialized;
    private static readonly AssemblyName Expected = new("NVorbis, Version=0.8.5.0, Culture=neutral, PublicKeyToken=null");

    internal static void Initialize()
    {
        lock (Sync)
        {
            if (initialized) return;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try
            {
                decoder = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => Matches(a.GetName())) ?? Load();
                initialized = true;
            }
            catch
            {
                AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
                throw;
            }
        }
    }

    private static bool Matches(AssemblyName name) => name.Name == Expected.Name && name.Version == Expected.Version &&
        string.IsNullOrEmpty(name.CultureName) && (name.GetPublicKeyToken()?.Length ?? 0) == 0;

    private static Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        if (!Matches(new AssemblyName(args.Name))) return null;
        lock (Sync) return decoder ??= Load();
    }

    private static Assembly Load()
    {
        using Stream stream = typeof(EmbeddedVorbis).Assembly.GetManifestResourceStream("SCP294.Dependencies.NVorbis.dll")
            ?? throw new FileNotFoundException("内嵌 NVorbis 解码器缺失。");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        Assembly loaded = Assembly.Load(buffer.ToArray());
        if (!Matches(loaded.GetName())) throw new FileLoadException("内嵌 NVorbis 版本不匹配。");
        return loaded;
    }

    internal static void Shutdown()
    {
        lock (Sync)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            initialized = false;
            decoder = null;
        }
    }
}
