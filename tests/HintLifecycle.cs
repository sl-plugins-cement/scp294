using System;
using System.Collections.Generic;
using System.Linq;
using HintServiceMeow.Core.Utilities;
using LabApi.Features.Wrappers;
using UnityEngine;

// This isolates QLZ's lifecycle adapter from Unity. It verifies expiry, object identity
// and layout without claiming to simulate the client or HSM's rendering engine.
internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++;
        System.Console.WriteLine("PASS " + name);
    }
    public static void Main()
    {
        var player = new Player();
        var display = PlayerDisplay.Get(player.ReferenceHub);
        Qlz.Hints.Reset();
        Time.realtimeSinceStartup = 0f;
        Qlz.Hints.Show(player, "message", "used", 560, 23, 6);
        object original = display.Active.Single();
        Qlz.Hints.Tick(5.99f);
        Check(display.Active.Count == 1, "notice survives before deadline");
        Qlz.Hints.Tick(6f);
        Check(display.Active.Count == 0 && display.Removed.Contains(original), "notice expires using original hint object");

        Time.realtimeSinceStartup = 10f;
        Qlz.Hints.Show(player, "message", "first", 560, 23, 6);
        original = display.Active.Single();
        Time.realtimeSinceStartup = 13f;
        Qlz.Hints.Show(player, "message", "second", 560, 23, 6);
        Qlz.Hints.Tick(16f);
        Check(display.Active.Count == 1 && ReferenceEquals(original, display.Active.Single()), "new notice reuses object and renews expiry");
        Check(display.Active.Single().Text == "second", "reused notice updates text");
        Qlz.Hints.Tick(19f);
        Check(display.Active.Count == 0, "renewed notice expires at new deadline");

        Check(Qlz.BuffLabel.Timed("巧乐兹", 4.1f) == "巧乐兹[00:05]", "unified compact countdown rounds up remaining time");
        Check(Qlz.BuffLabel.Timed("美味流星", -1f) == "美味流星[00:00]", "countdown never becomes negative");
        Check(Qlz.BuffLabel.Timed("5号化合物", 60f) == "5号化合物[01:00]", "backlash minute uses same compact countdown");
        Qlz.Hints.Show(player, "buff-time", "67[01:07]", 760, 23, 0, -260, "Right");
        Qlz.Hints.Show(player, "buff-ammo", "67 弹匣：67 / 67", 798, 22, 0, -260, "Right");
        Qlz.Hints.Show(player, "item-name", "vodka", 790, 26, 0);
        Qlz.Hints.Show(player, "item-intro", "intro", 828, 22, 0);
        Qlz.Hints.Show(player, "message", "line 1\nline 2", 560, 23, 6);
        Qlz.Hints.Tick(1000f);
        Check(display.Active.Count == 4, "persistent HUD survives while temporary message expires");
        original = display.Active.Single(h => h.Id == "qlz.buff-time");
        Qlz.Hints.Show(player, "buff-time", "67[01:06]", 760, 23, 0, -260, "Right");
        Check(display.Active.Count == 4 && ReferenceEquals(original, display.Active.Single(h => h.Id == "qlz.buff-time")), "countdown updates without duplicate AddHint");
        var buff = display.Active.Single(h => h.Id == "qlz.buff-time");
        Check(buff.Alignment == HintServiceMeow.Core.Enum.HintAlignment.Right && buff.XCoordinate == -260 && buff.YCoordinate == 760,
            "countdown is right aligned with inward/upward offset");
        Qlz.Hints.Show(player, "buff-time", "67[01:06]", 750, 23, 0, -280, "Right");
        Check(ReferenceEquals(original, buff) && buff.XCoordinate == -280 && buff.YCoordinate == 750,
            "existing HUD object can be repositioned without re-adding it");
        Qlz.Hints.Show(player, "buff-time", "67[01:06]", 760, 23, 0, -260, "Right");
        Check(display.Active.All(h => h.YCoordinateAlign == HintServiceMeow.Core.Enum.HintVerticalAlign.Top), "HUD uses top alignment");
        // The central intro and right countdown use separate columns; compare rows within
        // each column so their intentionally similar vertical coordinates don't collide.
        foreach (var column in display.Active.GroupBy(h => h.Alignment))
        {
            var rows = column.OrderBy(h => h.YCoordinate).ToArray();
            for (int i = 1; i < rows.Length; i++)
            {
                var prev = rows[i - 1];
                float height = prev.Text.Split('\n').Length * (prev.FontSize + prev.LineHeight);
                Check(prev.YCoordinate + height < rows[i].YCoordinate, "separate HUD row " + rows[i].Id);
            }
        }
        Qlz.Hints.Remove(player, "item-name");
        Qlz.Hints.Remove(player, "item-intro");
        Qlz.Hints.Remove(player, "item-detail");
        Check(display.Active.Count == 2, "holstering clears item description and preserves buff");
        Qlz.Hints.Clear(player);
        Check(display.Active.Count == 0, "player cleanup clears all own rows");

        Qlz.Hints.Show(player, "message", "reset", lifetime: 6);
        Qlz.Hints.Reset();
        Check(display.Active.Count == 0, "round/unload reset removes active hints");
        System.Console.WriteLine("Passed " + checks + " lifecycle/layout checks.");
    }
}

public class ReferenceHub { }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace LabApi.Features.Wrappers
{
    public class Player
    {
        public ReferenceHub ReferenceHub { get; } = new();
        public void SendHint(string text, float duration) => throw new Exception("Unexpected native fallback");
    }
}
namespace LabApi.Features.Console
{
    public static class Logger { public static void Warn(string text) => throw new Exception(text); }
}
namespace HintServiceMeow.Core.Enum
{
    public enum HintAlignment { Center, Right }
    public enum HintVerticalAlign { Top, Middle }
    public enum HintSyncSpeed { Fast }
}
namespace HintServiceMeow.Core.Models.Hints
{
    public abstract class AbstractHint
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int FontSize { get; set; }
        public float LineHeight { get; set; }
        public Core.Enum.HintSyncSpeed SyncSpeed { get; set; }
    }
    public class Hint : AbstractHint
    {
        public float XCoordinate { get; set; }
        public float YCoordinate { get; set; }
        public Core.Enum.HintAlignment Alignment { get; set; }
        public Core.Enum.HintVerticalAlign YCoordinateAlign { get; set; }
    }
}
namespace HintServiceMeow.Core.Utilities
{
    public class PlayerDisplay
    {
        private static readonly Dictionary<ReferenceHub, PlayerDisplay> Displays = new();
        public readonly List<Models.Hints.Hint> Active = new();
        public readonly List<object> Removed = new();
        public static PlayerDisplay Get(ReferenceHub hub)
        {
            if (!Displays.TryGetValue(hub, out var display)) Displays[hub] = display = new();
            return display;
        }
        public void AddHint(Models.Hints.AbstractHint hint, string group) => Active.Add((Models.Hints.Hint)hint);
        public void RemoveHint(Models.Hints.AbstractHint hint, string group)
        {
            if (!Active.Remove((Models.Hints.Hint)hint)) throw new Exception("Removed wrong hint instance");
            Removed.Add(hint);
        }
    }
}
