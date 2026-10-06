using System;
using System.Reflection;
using LabApi.Features.Wrappers;

namespace Qlz;

/// <summary>Optional ReinforcementsSystem lookup; without that plugin nobody belongs to a force.</summary>
internal sealed class ReinforcementForces
{
    private Func<Player, string?>? getForceId;
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

    public string? GetForceId(Player player)
    {
        Resolve();
        return getForceId?.Invoke(player);
    }

    private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) => needsResolve = true;

    private void Resolve()
    {
        if (!needsResolve) return;
        needsResolve = false;
        getForceId = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != "ReinforcementsSystem") continue;
            MethodInfo? method = assembly.GetType("ReinforcementsSystem.ReinforcementsSystemPlugin")?
                .GetMethod("GetReinforcementForceId", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Player) }, null);
            if (method != null && method.ReturnType == typeof(string))
                getForceId = (Func<Player, string?>)Delegate.CreateDelegate(typeof(Func<Player, string?>), method);
            break;
        }
    }
}
