using System;
using System.Reflection;
using System.Linq;
using LabApi.Loader;
using LabApi.Features.Wrappers;

namespace Qlz;

/// <summary>Optional ReinforcementsSystem lookup; without that plugin nobody belongs to a force.</summary>
internal sealed class ReinforcementForces
{
    private Func<Player, string?>? getForceId;
    private Func<Player, bool>? grantSunshine;
    private Func<bool>? daybreakActive;
    private Type? resolvedType;
    private bool needsResolve = true;

    public void Enable()
    {
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        needsResolve = true;
    }

    public void Disable()
    {
        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        getForceId = null;
        needsResolve = true;
    }

    public bool TryGrantSunshine(Player player) { Resolve(); return grantSunshine?.Invoke(player) == true; }
    public bool IsDaybreakActive() { Resolve(); return daybreakActive?.Invoke() == true; }

    public string? GetForceId(Player player)
    {
        Resolve();
        return getForceId?.Invoke(player);
    }

    private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) => needsResolve = true;

    private void Resolve()
    {
        Type? current = PluginLoader.EnabledPlugins.FirstOrDefault(p => p.GetType().Assembly.GetName().Name == "ReinforcementsSystem")?.GetType();
        if (!needsResolve && current == resolvedType) return;
        needsResolve = false;
        resolvedType = current;
        getForceId = null; grantSunshine = null; daybreakActive = null;
        if (current == null) return;
        MethodInfo? force = current.GetMethod("GetReinforcementForceId", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Player) }, null);
        if (force?.ReturnType == typeof(string)) getForceId = (Func<Player, string?>)Delegate.CreateDelegate(typeof(Func<Player, string?>), force);
        MethodInfo? grant = current.GetMethod("TryGrantSunshine", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Player) }, null);
        if (grant?.ReturnType == typeof(bool)) grantSunshine = (Func<Player, bool>)Delegate.CreateDelegate(typeof(Func<Player, bool>), grant);
        MethodInfo? active = current.GetMethod("IsDaybreakActive", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        if (active?.ReturnType == typeof(bool)) daybreakActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), active);
    }
}
