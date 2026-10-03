using System;
using InventorySystem.Items.Firearms.Modules;
using LabApi.Features.Wrappers;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS " + name);
    }

    private static FirearmItem Gun(params ModuleBase[] modules) => new() { Base = new() { Modules = modules } };

    public static void Main()
    {
        var magazine = new MagazineModule { AmmoStored = 0, MagazineInserted = false };
        var action = new AutomaticActionModule { Magazine = magazine, BoltLocked = true, Cocked = false };
        var gun = Gun(magazine, action);
        Check(Qlz.WeaponAmmo67.TryFill(gun), "empty gun receives ammunition");
        Check(magazine.MagazineInserted && action.Cocked && !action.BoltLocked && action.AmmoStored == 1,
            "empty locked gun gets magazine, chamber, cocking and unlocked bolt");
        Check(magazine.AmmoStored + action.AmmoStored == 67 && action.SyncCount == 1,
            "67 total rounds with native action synchronization");
        action.AmmoStored = 1; magazine.AmmoStored = 15;
        Check(Qlz.WeaponAmmo67.TryFill(gun) && magazine.AmmoStored + action.AmmoStored == 67,
            "refilling loaded gun doesn't add extra chamber rounds");
        action.OpenBolt = true;
        Check(Qlz.WeaponAmmo67.TryFill(gun) && action.AmmoStored == 0 && magazine.AmmoStored == 67 && action.Cocked,
            "open bolt has 67 magazine rounds and zero chamber rounds");
        action.OpenBolt = false; action.ChamberSize = 2;
        Check(Qlz.WeaponAmmo67.TryFill(gun) && action.ChamberSize == 2 && action.AmmoStored == 2 && magazine.AmmoStored == 65,
            "native multiple chamber capacity preserved");

        var reloader = new Reloader { IsReloadingOrUnloading = true };
        gun = Gun(magazine, action, reloader); magazine.AmmoStored = 0; magazine.MagazineInserted = false;
        Check(!Qlz.WeaponAmmo67.TryFill(gun) && !magazine.MagazineInserted && magazine.AmmoStored == 0,
            "ongoing reload isn't modified");
        Check(Qlz.WeaponAmmo67.TryFill(gun, reloadCompleted: true) && magazine.AmmoStored + action.AmmoStored == 67,
            "reload completion fills despite LabAPI's still-active reload flag");
        reloader.IsReloadingOrUnloading = false;
        Check(Qlz.WeaponAmmo67.TryFill(gun), "deferred newly acquired gun can be retried when idle");

        var pump = new PumpActionModule { Magazine = magazine, AmmoStored = 2 };
        Check(Qlz.WeaponAmmo67.TryFill(Gun(magazine, pump)) && pump.AmmoStored == 2 && magazine.AmmoStored == 65
            && pump.SyncCocked == 2 && pump.SyncCount == 1, "shotgun uses native pump with 67 total rounds");
        var cylinder = new CylinderAmmoModule();
        Check(Qlz.WeaponAmmo67.TryFill(Gun(cylinder)) && cylinder.AmmoStored == 6 && cylinder.SyncCount == 1,
            "fixed cylinder filled and synced within its safe native capacity");
        var revolver = Gun(cylinder);
        Check(Qlz.WeaponAmmo67.UsesVirtualMagazine(revolver), "revolver selects 67-shot virtual magazine");
        var virtualMagazine = new Qlz.VirtualMagazine();
        for (int shot = 1; shot <= 67; shot++)
        {
            virtualMagazine.Shot();
            if (!virtualMagazine.RefillPending || virtualMagazine.Remaining != 67 - shot) throw new Exception("incorrect virtual magazine count");
            Qlz.WeaponAmmo67.RefillVirtualMagazine(revolver, virtualMagazine.Remaining);
            var chambers = CylinderAmmoModule.GetChambersArrayForSerial(revolver.Serial, 6);
            if (virtualMagazine.Remaining > 0 && chambers[1].ServerSyncState != CylinderAmmoModule.ChamberState.Live)
                throw new Exception("next firing chamber is empty");
            virtualMagazine.RefillCommitted();
        }
        Check(virtualMagazine.Remaining == 0 && cylinder.AmmoStored == 0 && !virtualMagazine.RefillPending,
            "67 revolver shots exhaust virtual magazine and physical cylinder");
        virtualMagazine.Reload();
        Qlz.WeaponAmmo67.RefillVirtualMagazine(revolver, virtualMagazine.Remaining);
        Check(virtualMagazine.Remaining == 67 && cylinder.AmmoStored == 6, "revolver reload restores all 67 shots");
        Check(!Qlz.WeaponAmmo67.UsesVirtualMagazine(gun), "COM15-style magazine gun uses real ammo counter");

        var timeline = new Qlz.DrinkTimeline(15);
        Check(!timeline.TryStartBacklash(14.99f, 60) && !timeline.Backlash, "Compound V retains speed until 15 seconds");
        Check(timeline.TryStartBacklash(15, 60) && timeline.Backlash && timeline.EndAt == 75,
            "Compound V transitions to 60-second backlash");
        Check(!timeline.TryStartBacklash(20, 60) && !timeline.Expired(74.99f) && timeline.Expired(75),
            "backlash can't restart and expires after one minute");
        var delayed = new Qlz.DrinkTimeline(15);
        Check(delayed.TryStartBacklash(16, 60) && delayed.EndAt == 75, "late update doesn't extend backlash lifetime");
        var meteor = new Qlz.MeteorMotion(0);
        Check(!meteor.ShouldExplode(1f, 0, 1), "meteor grants a full one-second starting grace");
        Check(meteor.ShouldExplode(1.01f, 0, 1), "meteor explodes only after stillness exceeds one second");
        meteor = new Qlz.MeteorMotion(0);
        Check(!meteor.ShouldExplode(0.9f, 2, 1) && !meteor.ShouldExplode(1.8f, 0, 1), "actual motion resets stillness timer");
        Check(meteor.ShouldExplode(1.91f, 0.09f, 1), "tiny jitter cannot postpone explosion");
        Check(!Qlz.WeaponAmmo67.TryFill(Gun()), "missing ammo container safely ignored");
        Console.WriteLine($"Passed {checks} weapon adapter checks.");
    }
}

// These model the inspected native API contracts. They don't simulate Unity networking.
namespace LabApi.Features.Wrappers
{
    public class Firearm { public ModuleBase[] Modules { get; set; } = Array.Empty<ModuleBase>(); }
    public class FirearmItem { public Firearm Base { get; set; } = new(); public ushort Serial => 1; }
}
namespace InventorySystem.Items.Firearms.Modules
{
    public class ModuleBase { }
    public interface IReloaderModule { bool IsReloadingOrUnloading { get; } }
    public class Reloader : ModuleBase, IReloaderModule { public bool IsReloadingOrUnloading { get; set; } }
    public interface IPrimaryAmmoContainerModule
    {
        int AmmoStored { get; }
        int AmmoMax { get; }
        void ServerModifyAmmo(int amount);
    }
    public class MagazineModule : ModuleBase, IPrimaryAmmoContainerModule
    {
        public int AmmoStored { get; set; }
        public int AmmoMax => 30;
        public bool MagazineInserted { get; set; } = true;
        public void ServerInsertEmptyMagazine() => MagazineInserted = true;
        public void ServerModifyAmmo(int amount) { if (MagazineInserted) AmmoStored += amount; }
    }
    public class AutomaticActionModule : ModuleBase
    {
        public MagazineModule Magazine { get; set; } = new();
        public int AmmoStored { get; set; }
        public int ChamberSize { get; set; } = 1;
        public bool OpenBolt { get; set; }
        public bool BoltLocked { get; set; }
        public bool Cocked { get; set; }
        public int SyncCount;
        public void ServerCycleAction()
        {
            Cocked = true;
            if (!OpenBolt)
            {
                int amount = Math.Min(Magazine.AmmoStored, ChamberSize - AmmoStored);
                AmmoStored += amount;
                Magazine.ServerModifyAmmo(-amount);
                BoltLocked = AmmoStored == 0;
            }
            SyncCount++;
        }
    }
    public class PumpActionModule : ModuleBase
    {
        public MagazineModule Magazine { get; set; } = new();
        public int AmmoStored { get; set; }
        public int SyncCocked;
        public int SyncCount;
        public void Pump()
        {
            SyncCocked = 2;
            AmmoStored = Math.Min(Magazine.AmmoStored, 2);
            Magazine.ServerModifyAmmo(-AmmoStored);
            SyncCount++;
        }
    }
    public class CylinderAmmoModule : ModuleBase, IPrimaryAmmoContainerModule
    {
        public enum ChamberState { Empty, Live, Discharged }
        public class Chamber { public ChamberState ServerSyncState; }
        private static readonly Chamber[] chambers = new Chamber[] { new(), new(), new(), new(), new(), new() };
        public static Chamber[] GetChambersArrayForSerial(ushort serial, int capacity) => chambers;
        public int AmmoStored => System.Linq.Enumerable.Count(chambers, c => c.ServerSyncState == ChamberState.Live);
        public int AmmoMax => 6;
        public int SyncCount;
        public void ServerModifyAmmo(int amount)
        {
            foreach (var chamber in chambers)
            {
                if (amount > 0 && chamber.ServerSyncState != ChamberState.Live) { chamber.ServerSyncState = ChamberState.Live; amount--; }
                else if (amount < 0 && chamber.ServerSyncState == ChamberState.Live) { chamber.ServerSyncState = ChamberState.Empty; amount++; }
            }
        }
        public void ServerResync() => SyncCount++;
    }
}
