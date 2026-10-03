using System;
using System.Linq;
using InventorySystem.Items.Firearms.Modules;
using LabApi.Features.Wrappers;

namespace Qlz;

internal static class WeaponAmmo67
{
    internal static bool TryFill(FirearmItem firearm, bool reloadCompleted = false)
    {
        var modules = firearm.Base.Modules;
        // Don't insert magazines or cycle actions in the middle of a reload/unload.
        if (!reloadCompleted && modules.OfType<IReloaderModule>().Any(m => m.IsReloadingOrUnloading))
            return false;
        var container = modules.OfType<IPrimaryAmmoContainerModule>().FirstOrDefault();
        if (container == null) return false;

        if (container is MagazineModule magazine)
        {
            if (!magazine.MagazineInserted) magazine.ServerInsertEmptyMagazine();
            var automatic = modules.OfType<AutomaticActionModule>().FirstOrDefault();
            if (automatic != null)
            {
                // The chamber count shares a packed byte with cocked/bolt flags; 67 cannot
                // go there. Keep the native capacity and let cycling load a normal chamber.
                automatic.AmmoStored = 0;
                automatic.BoltLocked = false;
                magazine.ServerModifyAmmo(67 - magazine.AmmoStored);
                automatic.ServerCycleAction(); // Cocks, chambers, and syncs to the client.
                return true;
            }
            var pump = modules.OfType<PumpActionModule>().FirstOrDefault();
            if (pump != null)
            {
                pump.AmmoStored = 0;
                magazine.ServerModifyAmmo(67 - magazine.AmmoStored);
                pump.Pump(); // Native barrel count and hammer cocking, including RPC sync.
                return true;
            }
            magazine.ServerModifyAmmo(67 - magazine.AmmoStored);
            return true;
        }

        // Revolvers receive a 67-shot virtual magazine maintained by the plugin. Their
        // physical cylinder must stay native-sized because clients index chamber arrays.
        int target = Math.Min(67, Math.Max(0, container.AmmoMax));
        container.ServerModifyAmmo(target - container.AmmoStored);
        if (container is CylinderAmmoModule cylinder) cylinder.ServerResync();
        return true;
    }

    internal static bool UsesVirtualMagazine(FirearmItem firearm) =>
        firearm.Base.Modules.Any(m => m is CylinderAmmoModule);

    internal static void RefillVirtualMagazine(FirearmItem firearm, int remaining)
    {
        var cylinder = firearm.Base.Modules.OfType<CylinderAmmoModule>().FirstOrDefault();
        if (cylinder == null) return;
        int target = Math.Min(Math.Max(0, remaining), cylinder.AmmoMax);
        var chambers = CylinderAmmoModule.GetChambersArrayForSerial(firearm.Serial, cylinder.AmmoMax);
        // After a native shot, double action rotates chamber 1 into the firing position.
        // Keep the remaining rounds contiguous from there, avoiding dry gaps near zero.
        for (int i = 0; i < cylinder.AmmoMax; i++) chambers[i].ServerSyncState = CylinderAmmoModule.ChamberState.Empty;
        for (int i = 0; i < target; i++) chambers[(i + 1) % cylinder.AmmoMax].ServerSyncState = CylinderAmmoModule.ChamberState.Live;
        cylinder.ServerResync();
    }
}
