using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LabApi.Features.Wrappers;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace Qlz;

internal static class Hints
{
    private const string Group = "qlz";
    private sealed class Entry
    {
        public object Display = null!;
        public object Hint = null!;
        public string Text = string.Empty;
        public float ExpiresAt;
    }
    private static readonly Dictionary<(Player Player, string Id), Entry> Active = new();
    private static Type? hintType;
    private static MethodInfo? getDisplay, add, remove;
    private static PropertyInfo? textProperty;
    private static bool ready, failed, warned;

    // Zero lifetime is only for persistent HUD rows, removed explicitly by their owner.
    public static void Show(Player player, string id, string text, float y = 560f,
        int fontSize = 24, float lifetime = 6f, float x = 0f, string alignment = "Center")
    {
        if (player == null || player.ReferenceHub == null) return;
        if (!TryInitialize())
        {
            // Multiple native hints overwrite one another, so only use this for notices.
            if (lifetime > 0) player.SendHint(text, lifetime);
            return;
        }
        try
        {
            var key = (player, id);
            if (Active.TryGetValue(key, out Entry? entry))
            {
                if (entry.Text != text)
                {
                    textProperty!.SetValue(entry.Hint, text);
                    entry.Text = text;
                }
                entry.ExpiresAt = lifetime > 0 ? Time.realtimeSinceStartup + lifetime : float.PositiveInfinity;
                Set(entry.Hint, "XCoordinate", x);
                Set(entry.Hint, "YCoordinate", y);
                Set(entry.Hint, "FontSize", fontSize);
                SetEnum(entry.Hint, "Alignment", alignment);
                return;
            }
            object display = getDisplay!.Invoke(null, new object[] { player.ReferenceHub })!;
            object hint = Activator.CreateInstance(hintType!)!;
            Set(hint, "Id", "qlz." + id);
            Set(hint, "Text", text);
            Set(hint, "XCoordinate", x);
            Set(hint, "YCoordinate", y);
            Set(hint, "FontSize", fontSize);
            Set(hint, "LineHeight", 6f);
            SetEnum(hint, "Alignment", alignment);
            SetEnum(hint, "YCoordinateAlign", "Top");
            SetEnum(hint, "SyncSpeed", "Fast");
            add!.Invoke(display, new[] { hint, Group });
            Active[key] = new Entry
            {
                Display = display, Hint = hint, Text = text,
                ExpiresAt = lifetime > 0 ? Time.realtimeSinceStartup + lifetime : float.PositiveInfinity,
            };
        }
        catch (Exception ex)
        {
            Warn("HSM 显示失败：" + ex.GetBaseException().Message);
            if (lifetime > 0) player.SendHint(text, lifetime);
        }
    }

    public static void Tick(float now)
    {
        foreach (var pair in Active.Where(p => p.Value.ExpiresAt <= now).ToArray())
            Remove(pair.Key.Player, pair.Key.Id);
    }
    public static void Remove(Player player, string id)
    {
        var key = (player, id);
        if (!Active.TryGetValue(key, out Entry? entry)) return;
        RemoveEntry(entry);
        Active.Remove(key);
    }
    public static void Clear(Player player)
    {
        foreach (var key in Active.Keys.Where(k => k.Player == player).ToArray()) Remove(player, key.Id);
    }
    public static void ClearAll()
    {
        foreach (Entry entry in Active.Values) RemoveEntry(entry);
        Active.Clear();
    }
    public static void Reset()
    {
        ClearAll();
        ready = failed = warned = false;
    }
    private static void RemoveEntry(Entry entry)
    {
        try { remove!.Invoke(entry.Display, new[] { entry.Hint, Group }); }
        catch (Exception ex) { Warn("HSM 移除失败：" + ex.GetBaseException().Message); }
    }
    private static void Set(object hint, string name, object value) => hintType!.GetProperty(name)!.SetValue(hint, value);
    private static void SetEnum(object hint, string name, string value)
    {
        PropertyInfo property = hintType!.GetProperty(name)!;
        property.SetValue(hint, Enum.Parse(property.PropertyType, value));
    }
    private static bool TryInitialize()
    {
        if (ready) return true;
        if (failed) return false;
        Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
            a.GetName().Name?.StartsWith("HintServiceMeow", StringComparison.OrdinalIgnoreCase) == true);
        // Retry later if HSM loads after QLZ.
        if (assembly == null) { Warn("未加载 HSM，暂时仅显示原生短提示。"); return false; }
        try
        {
            Type display = assembly.GetType("HintServiceMeow.Core.Utilities.PlayerDisplay", true)!;
            Type abstractHint = assembly.GetType("HintServiceMeow.Core.Models.Hints.AbstractHint", true)!;
            hintType = assembly.GetType("HintServiceMeow.Core.Models.Hints.Hint", true)!;
            getDisplay = display.GetMethod("Get", new[] { typeof(ReferenceHub) });
            add = display.GetMethod("AddHint", new[] { abstractHint, typeof(string) });
            remove = display.GetMethod("RemoveHint", new[] { abstractHint, typeof(string) });
            textProperty = abstractHint.GetProperty("Text");
            ready = getDisplay != null && add != null && remove != null && textProperty != null;
            failed = !ready;
            if (failed) Warn("HSM 版本不兼容，暂时仅显示原生短提示。");
            return ready;
        }
        catch (Exception ex) { failed = true; Warn("HSM 初始化失败：" + ex.GetBaseException().Message); return false; }
    }
    private static void Warn(string message)
    {
        if (warned) return;
        warned = true;
        Logger.Warn("[QLZ] " + message);
    }
}
