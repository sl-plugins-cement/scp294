using System;

namespace Qlz;

internal static class BuffLabel
{
    public static string Timed(string name, float remaining)
    {
        int seconds = Math.Max(0, (int)Math.Ceiling(remaining));
        return $"{name}[{seconds / 60:00}:{seconds % 60:00}]";
    }
}
