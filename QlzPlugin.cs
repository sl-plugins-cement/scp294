using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using MEC;
using PlayerStatsSystem;
using PlayerRoles;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace Qlz;

public sealed class QlzPlugin : Plugin<Config>
{
    public static QlzPlugin? Instance { get; private set; }
    public override string Name => "SCP-294？";
    public override string Description => "固定位置的巨大 SCP-207 搞怪饮料机。";
    public override string Author => "Codex";
    public override Version Version => new(0, 5, 4);
    public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);

    private static readonly Drink[] DrinkPool = (Drink[])Enum.GetValues(typeof(Drink));
    private readonly Dictionary<ushort, Drink> drinks = new();
    private readonly Dictionary<int, float> nextUse = new();
    private readonly LifeBottleQuota bottleQuota = new();
    private const string BottleLimitMessage = "<color=#FFD447>本条生命已经领过一瓶饮料啦，下条生命再来品尝吧！</color>";
    private readonly Dictionary<int, BuffState> buffs = new();
    private Pickup? machine;
    private CoroutineHandle tick;
    private float spawnAt = float.PositiveInfinity;

    private sealed class BuffState
    {
        public Drink Drink;
        public float EndAt;
        public float BaseMaxHealth;
        public float BaseMaxHume;
        public float BaseHume;
        public Vector3 BaseScale;
        public readonly HashSet<ushort> ModifiedWeapons = new();
        public readonly Dictionary<ushort, VirtualMagazine> VirtualMagazines = new();
        public DrinkTimeline? Timeline;
        public MeteorMotion? Motion;
        public JiahaoAudio? Music;
        public string HolderName = string.Empty;
        public bool Restoring;
        public bool QiaoLeZiCardiac;
        public AhpProcess? Ahp;
        public bool AheadDebuffApplied;
        public int LifeId;
        public readonly Dictionary<CustomPlayerEffects.StatusEffectBase, (byte Intensity, float EndAt)> Effects = new();
    }

    public override void Enable()
    {
        Instance = this;
        if (!Config!.IsEnabled)
        {
            Logger.Info("[SCP-294?] Disabled by config.");
            return;
        }

        Hints.Reset();
        PlayerEvents.SearchingPickup += OnSearchingPickup;
        PlayerEvents.PickingUpItem += OnPickingUpItem;
        PlayerEvents.UsingItem += OnUsingItem;
        PlayerEvents.ChangingItem += OnChangingItem;
        PlayerEvents.DroppingItem += OnDroppingItem;
        PlayerEvents.Hurting += OnHurting;
        PlayerEvents.UpdatingEffect += OnUpdatingEffect;
        PlayerEvents.ReloadedWeapon += OnReloadedWeapon;
        PlayerEvents.ShotWeapon += OnShotWeapon;
        PlayerEvents.Left += OnLeft;
        PlayerEvents.ChangedRole += OnChangedRole;
        PlayerRoleManager.OnServerRoleSet += OnServerRoleSet;
        PlayerEvents.Death += OnDeath;
        ServerEvents.RoundStarted += OnRoundStarted;
        ServerEvents.RoundRestarted += OnRoundReset;
        ServerEvents.WaitingForPlayers += OnRoundReset;
        tick = Timing.RunCoroutine(Tick());
        JiahaoAudio.Preload(MusicPath);
        if (Config.SpawnOnRoundStart && Round.IsRoundInProgress)
            spawnAt = Time.realtimeSinceStartup + Math.Max(0f, Config.SpawnDelay - (float)Round.Duration.TotalSeconds);
        Logger.Info("[SCP-294?] Loaded. Use scp294 spawn to place the fixed 10x SCP-207 machine.");
    }

    public override void Disable()
    {
        Timing.KillCoroutines(tick);
        PlayerEvents.SearchingPickup -= OnSearchingPickup;
        PlayerEvents.PickingUpItem -= OnPickingUpItem;
        PlayerEvents.UsingItem -= OnUsingItem;
        PlayerEvents.ChangingItem -= OnChangingItem;
        PlayerEvents.DroppingItem -= OnDroppingItem;
        PlayerEvents.Hurting -= OnHurting;
        PlayerEvents.UpdatingEffect -= OnUpdatingEffect;
        PlayerEvents.ReloadedWeapon -= OnReloadedWeapon;
        PlayerEvents.ShotWeapon -= OnShotWeapon;
        PlayerEvents.Left -= OnLeft;
        PlayerEvents.ChangedRole -= OnChangedRole;
        PlayerRoleManager.OnServerRoleSet -= OnServerRoleSet;
        PlayerEvents.Death -= OnDeath;
        ServerEvents.RoundStarted -= OnRoundStarted;
        ServerEvents.RoundRestarted -= OnRoundReset;
        ServerEvents.WaitingForPlayers -= OnRoundReset;
        ClearMachine();
        spawnAt = float.PositiveInfinity;
        foreach (BuffState state in buffs.Values) state.Music?.Stop();
        JiahaoAudio.Shutdown();
        foreach (Player player in Player.List)
        {
            if (buffs.TryGetValue(player.PlayerId, out BuffState state) && player.LifeId == state.LifeId)
                RestoreBase(player, state);
            Hints.Clear(player);
        }
        Hints.ClearAll();
        drinks.Clear();
        buffs.Clear();
        nextUse.Clear();
        bottleQuota.Reset();
        Instance = null;
    }

    internal string SpawnCommand()
    {
        SpawnMachine();
        return machine == null ? "大型 207 生成失败。" : $"大型 207 饮料机已生成：{Config!.MachinePosition}";
    }

    internal string ClearCommand()
    {
        ClearMachine();
        return "大型 207 饮料机已清除。";
    }

    internal bool BuffsAvailable => Config?.IsEnabled == true;

    internal string GiveBuff(Player player, BuffGrant grant)
    {
        ApplyDrink(player, grant.Drink, grant.Duration, grant.Branch);
        string prefix = $"{player.Nickname}({player.PlayerId})：";
        return prefix + (player.IsAlive ? GetBuffDescription(player) : "已触发嘉豪失败分支，玩家死亡。");
    }

    internal string ClearBuff(Player player)
    {
        if (!buffs.TryGetValue(player.PlayerId, out BuffState state))
            return $"{player.Nickname}({player.PlayerId})：没有饮料 Buff。";
        buffs.Remove(player.PlayerId);
        if (player.IsAlive && player.LifeId == state.LifeId) RestoreBase(player, state);
        else state.Music?.Stop();
        ClearBuffHud(player);
        Notice(player, $"<color=#CCCCCC>{DrinkName(state.Drink)}效果已被管理员清除</color>", 3f);
        return $"{player.Nickname}({player.PlayerId})：已清除 {DrinkName(state.Drink)}。";
    }

    internal string GetBuffStatus(Player player) => $"{player.Nickname}({player.PlayerId})：{GetBuffDescription(player)}";

    private string GetBuffDescription(Player player)
    {
        if (!buffs.TryGetValue(player.PlayerId, out BuffState state) || !player.IsAlive || player.LifeId != state.LifeId)
            return "没有饮料 Buff。";
        if (state.AheadDebuffApplied) return "遥遥领先：GOC 卡脖子，持续本条生命。";
        string phase = state.Drink == Drink.QiaoLeZi ? (state.QiaoLeZiCardiac ? "（心脏骤停）" : "（加速）") :
            state.Drink == Drink.CompoundV && state.Timeline?.Backlash == true ? "（反噬）" : string.Empty;
        return BuffLabel.Timed(DrinkName(state.Drink), state.EndAt - Time.realtimeSinceStartup) + phase;
    }

    private void OnRoundStarted()
    {
        bottleQuota.Reset();
        ClearMachine();
        spawnAt = Config!.SpawnOnRoundStart ? Time.realtimeSinceStartup + Math.Max(0f, Config.SpawnDelay) : float.PositiveInfinity;
    }
    private void OnRoundReset()
    {
        spawnAt = float.PositiveInfinity;
        foreach (BuffState state in buffs.Values) state.Music?.Stop();
        JiahaoAudio.StopAll();
        foreach (Player player in Player.List)
            if (buffs.TryGetValue(player.PlayerId, out BuffState state) && player.LifeId == state.LifeId)
                RestoreBase(player, state);
        Hints.ClearAll();
        ClearMachine(); buffs.Clear(); drinks.Clear(); nextUse.Clear();
        bottleQuota.Reset();
    }

    private void SpawnMachine()
    {
        ClearMachine();
        Vector3 position = Config!.MachinePosition;
        Quaternion rotation = Quaternion.Euler(Config.MachineRotation);
        machine = Pickup.Create(ItemType.SCP207, position, rotation, Vector3.one * Config.MachineScale, false);
        if (machine == null) return;
        // Keep the native Pickup searchable. Intercept only when its native progress completes.
        machine.IsLocked = false;
        if (machine.Rigidbody != null)
            machine.Rigidbody.isKinematic = true;
        machine.Spawn();
        if (machine.PickupStandardPhysics?.Rb != null)
            machine.PickupStandardPhysics.Rb.isKinematic = true;

        Server.SendBroadcast("[SCP-294?]已经出现在地表处，快来品尝吧😋", Config.SpawnBroadcastDuration);

        Logger.Info($"[SCP-294?] Machine spawned at {position} scale={Config.MachineScale}.");
    }

    private void ClearMachine()
    {
        if (machine != null && !machine.IsDestroyed) machine.Destroy();
        machine = null;
    }

    private void Dispense(Player player)
    {
        if (!player.IsAlive || !player.IsHuman || player.IsInventoryFull) return;
        // Recheck at progress completion so a second pending interaction cannot dispense.
        if (!bottleQuota.CanReceive(player.PlayerId, player.LifeId))
        {
            Notice(player, BottleLimitMessage, 3f);
            return;
        }
        float now = Time.realtimeSinceStartup;
        if (nextUse.TryGetValue(player.PlayerId, out float ready) && ready > now)
        {
            Notice(player, $"<color=#FFD34D>饮料机还在冷却：{ready - now:0.0} 秒</color>", 2f);
            return;
        }
        Drink drink = DrinkPool[UnityEngine.Random.Range(0, DrinkPool.Length)];
        Item? item = player.AddItem(ItemType.SCP207);
        if (item == null)
        {
            Notice(player, "<color=#FF6969>你的背包没有空间，饮料机拒绝出货。</color>");
            return;
        }
        drinks[item.Serial] = drink;
        bottleQuota.RecordReceived(player.PlayerId, player.LifeId);
        nextUse[player.PlayerId] = now + Config!.PersonalCooldown;
        Notice(player, $"<color=#FFD447>你获得了：{DrinkName(drink)}</color>\n<color=#CCCCCC>{DrinkIntro(drink)} · 手持可查看介绍</color>");
        Logger.Info($"[SCP-294?] {player.Nickname} received {drink} serial={item.Serial}.");
    }

    private void OnPickingUpItem(PlayerPickingUpItemEventArgs ev)
    {
        if (!IsMachine(ev.Pickup)) return;
        // ItemSearchCompletor raises this AFTER the original pickup bar finishes and resets
        // InUse when cancelled. Preserve the machine and give a separate random bottle.
        bool allowed = ev.IsAllowed;
        ev.IsAllowed = false;
        if (allowed) Dispense(ev.Player);
    }

    private bool IsMachine(Pickup pickup) => machine != null && !machine.IsDestroyed && pickup.Base == machine.Base;

    private void OnSearchingPickup(PlayerSearchingPickupEventArgs ev)
    {
        if (!IsMachine(ev.Pickup) || !ev.IsAllowed) return;
        if (ev.Player.IsAlive && ev.Player.IsHuman && !bottleQuota.CanReceive(ev.Player.PlayerId, ev.Player.LifeId))
        {
            ev.IsAllowed = false;
            Notice(ev.Player, BottleLimitMessage, 3f);
        }
        else if (!ev.Player.IsAlive || !ev.Player.IsHuman || ev.Player.IsInventoryFull)
        {
            ev.IsAllowed = false;
            Notice(ev.Player, "<color=#FF7777>请留出一个背包空位再领取饮料。</color>", 3f);
        }
        else if (nextUse.TryGetValue(ev.Player.PlayerId, out float ready) && ready > Time.realtimeSinceStartup)
        {
            ev.IsAllowed = false;
            Notice(ev.Player, $"<color=#FFD447>饮料机冷却：{ready - Time.realtimeSinceStartup:0.0} 秒</color>", 2f);
        }
    }

    private void OnUsingItem(PlayerUsingItemEventArgs ev)
    {
        if (IsMeteor(ev.Player)) { ev.IsAllowed = false; return; }
        if (ev.UsableItem == null || !drinks.TryGetValue(ev.UsableItem.Serial, out Drink drink)) return;
        ev.IsAllowed = false;
        Player player = ev.Player;
        if (!player.IsAlive) return;
        drinks.Remove(ev.UsableItem.Serial);
        player.RemoveItem(ev.UsableItem);
        ClearItemHud(player);
        ApplyDrink(player, drink);
    }

    private void ApplyDrink(Player player, Drink drink, float? duration = null, BuffBranch branch = BuffBranch.Random)
    {
        switch (drink)
        {
            case Drink.QiaoLeZi: ApplyQiaoLeZi(player, duration, branch); break;
            case Drink.SixtySeven: ApplySixtySeven(player, duration ?? Config!.SixtySevenDuration); break;
            case Drink.Ahead: ApplyAhead(player, duration ?? Config!.AheadDuration); break;
            case Drink.Vodka: ApplyVodka(player, duration ?? Config!.VodkaDuration); break;
            case Drink.CompoundV: ApplyCompoundV(player, duration ?? Config!.CompoundVDuration); break;
            case Drink.Meteor: ApplyMeteor(player, duration ?? Config!.MeteorDuration); break;
            case Drink.Jiahao: ApplyJiahao(player, duration ?? Config!.JiahaoDuration, branch); break;
        }
    }

    private bool IsMeteor(Player player) => buffs.TryGetValue(player.PlayerId, out BuffState state) &&
        state.Drink == Drink.Meteor && player.IsAlive && player.LifeId == state.LifeId;

    private void OnChangingItem(PlayerChangingItemEventArgs ev) { if (IsMeteor(ev.Player)) ev.IsAllowed = false; }
    private void OnDroppingItem(PlayerDroppingItemEventArgs ev) { if (IsMeteor(ev.Player)) ev.IsAllowed = false; }

    private BuffState Capture(Player player, Drink drink, float duration)
    {
        // First edition: one drink buff at a time. Replacing it cleans its HUD and modifiers.
        if (buffs.TryGetValue(player.PlayerId, out BuffState previous))
        {
            if (player.LifeId == previous.LifeId) RestoreBase(player, previous);
            else previous.Music?.Stop();
            ClearBuffHud(player);
        }
        var state = new BuffState
        {
            Drink = drink,
            EndAt = Time.realtimeSinceStartup + duration,
            BaseMaxHealth = player.MaxHealth,
            BaseMaxHume = player.MaxHumeShield,
            BaseHume = player.HumeShield,
            BaseScale = player.Scale,
            LifeId = player.LifeId,
        };
        foreach (var effect in new CustomPlayerEffects.StatusEffectBase?[]
            { player.GetEffect<CustomPlayerEffects.MovementBoost>(), player.GetEffect<CustomPlayerEffects.Slowness>(),
              player.GetEffect<CustomPlayerEffects.DamageReduction>(), player.GetEffect<CustomPlayerEffects.Burned>(),
              player.GetEffect<CustomPlayerEffects.Concussed>(), player.GetEffect<CustomPlayerEffects.Scp207>(),
              player.GetEffect<CustomPlayerEffects.CardiacArrest>(), player.GetEffect<CustomPlayerEffects.Scp1853>(),
              player.GetEffect<CustomPlayerEffects.Poisoned>() })
            if (effect != null)
                state.Effects[effect] = (effect.Intensity,
                    effect.Duration == 0f ? float.PositiveInfinity : Time.realtimeSinceStartup + effect.TimeLeft);
        buffs[player.PlayerId] = state;
        return state;
    }

    private void ApplyQiaoLeZi(Player player, float? requestedDuration, BuffBranch branch)
    {
        bool cardiac = branch == BuffBranch.Secondary || branch == BuffBranch.Random && UnityEngine.Random.Range(0, 2) == 0;
        float duration = requestedDuration ?? (cardiac ? Config!.QiaoLeZiCardiacDuration : Config!.QiaoLeZiDuration);
        BuffState state = Capture(player, Drink.QiaoLeZi, duration);
        state.QiaoLeZiCardiac = cardiac;
        if (cardiac)
        {
            player.EnableEffect<CustomPlayerEffects.CardiacArrest>(1, duration, false);
            Notice(player, "<color=#FF7777>我看你嘴唇发紫是不是心脏不好</color>");
            return;
        }
        player.EnableEffect<CustomPlayerEffects.MovementBoost>(100, duration, false);
        Notice(player, "<color=#FFD447>你好像获得了某位故人的天赋</color>");
    }

    private void ApplySixtySeven(Player player, float duration)
    {
        Capture(player, Drink.SixtySeven, duration);
        player.MaxHealth = 67f;
        player.Health = 67f;
        player.MaxHumeShield = 67f;
        player.HumeShield = 67f;
        buffs[player.PlayerId].Ahp = player.CreateAhpProcess(67f, 67f, 0f, 1f, duration, true);
        SetAmmoTo67(player, buffs[player.PlayerId]);
        Notice(player, "<color=#FFFFFF>你并不知道这一堆无意义的数字为何存在</color>");
    }

    private void ApplyAhead(Player player, float duration)
    {
        BuffState state = Capture(player, Drink.Ahead, duration);
        player.EnableEffect<CustomPlayerEffects.MovementBoost>(50, duration, false);
        Notice(player, "<color=#FFD447>遥遥领先，我们继续领先(*°▽°*)八(*°▽°*)♪</color>\n<color=#FF8A8A>我们领先于GOC 67？%，让GOC永远追不上！</color>");
        state.AheadDebuffApplied = false;
    }

    private void ApplyVodka(Player player, float duration)
    {
        Capture(player, Drink.Vodka, duration);
        player.Scale = new Vector3(1.15f, 0.8f, 1.15f);
        player.MaxHealth = 200f;
        player.Health = 200f;
        buffs[player.PlayerId].Ahp = player.CreateAhpProcess(150f, 150f, 0f, 1f, duration, true);
        player.EnableEffect<CustomPlayerEffects.Slowness>(10, duration, false);
        player.EnableEffect<CustomPlayerEffects.DamageReduction>(15, duration, false);
        Notice(player, "<color=#D9E8FF>你获得了某位强悍人物的视野</color>");
    }

    private void ApplyCompoundV(Player player, float duration)
    {
        BuffState state = Capture(player, Drink.CompoundV, duration);
        state.Timeline = new DrinkTimeline(state.EndAt);
        player.EnableEffect<CustomPlayerEffects.MovementBoost>(255, duration, false);
        Notice(player, "<color=#66D9FF>5号化合物入体：祖国人体验卡已到账！</color>");
    }

    private void ApplyMeteor(Player player, float duration)
    {
        BuffState state = Capture(player, Drink.Meteor, duration);
        player.EnableEffect<CustomPlayerEffects.Scp207>(3, duration, false);
        player.EnableEffect<CustomPlayerEffects.MovementBoost>(150, duration, false);
        state.Motion = new MeteorMotion(Time.realtimeSinceStartup);
        Notice(player, "<color=#FFB766>美味流星，出发！停下一秒就会燃尽。</color>");
    }

    private string MusicPath => Path.IsPathRooted(Config!.JiahaoMusicPath) ? Config.JiahaoMusicPath :
        Path.Combine(Path.GetDirectoryName(FilePath) ?? AppDomain.CurrentDomain.BaseDirectory, Config.JiahaoMusicPath);

    private void ApplyJiahao(Player player, float duration, BuffBranch branch)
    {
        if (branch == BuffBranch.Secondary || branch == BuffBranch.Random && UnityEngine.Random.Range(0, 10) != 0)
        {
            player.Kill("你凡人的身躯还不足以驾驭这份力量");
            Notice(player, "<color=#ECA0AC>你凡人的身躯，还不足以驾驭</color>\n<color=#FFD700><b>这份力量</b></color>", 5f);
            return;
        }
        BuffState state = Capture(player, Drink.Jiahao, duration);
        state.HolderName = player.Nickname;
        player.EnableEffect<CustomPlayerEffects.Scp1853>(3, duration, false);
        player.EnableEffect<CustomPlayerEffects.Scp207>(3, duration, false);
        state.Music = JiahaoAudio.Start(player, MusicPath, Config!.JiahaoMusicRadius, Config.JiahaoMusicVolume);
        GlobalNotice("<color=#FFD700><b>那个男人？</b></color>\n<color=#D8C8F0>难道又重出江湖了吗</color>", 5f);
    }

    private void GlobalNotice(string text, float duration = 5f)
    {
        foreach (Player player in Player.ReadyList)
            Hints.Show(player, "global-message", text, 370f, 25, duration);
    }

    private void SetAmmoTo67(Player player, BuffState state)
    {
        foreach (Item item in player.Items)
        {
            if (item is FirearmItem firearm)
            {
                if (state.ModifiedWeapons.Contains(firearm.Serial)) continue;
                if (!WeaponAmmo67.TryFill(firearm)) continue;
                state.ModifiedWeapons.Add(firearm.Serial);
                if (WeaponAmmo67.UsesVirtualMagazine(firearm)) state.VirtualMagazines[firearm.Serial] = new VirtualMagazine();
                if (firearm.AmmoType != ItemType.None) player.SetAmmo(firearm.AmmoType, 67);
            }
        }
    }

    private void OnReloadedWeapon(PlayerReloadedWeaponEventArgs ev)
    {
        if (buffs.TryGetValue(ev.Player.PlayerId, out BuffState state) && state.Drink == Drink.SixtySeven &&
            state.EndAt > Time.realtimeSinceStartup && ev.FirearmItem != null)
        {
            // LabAPI raises ReloadedWeapon before clearing the native reload flag.
            if (WeaponAmmo67.TryFill(ev.FirearmItem, reloadCompleted: true))
            {
                state.ModifiedWeapons.Add(ev.FirearmItem.Serial);
                if (WeaponAmmo67.UsesVirtualMagazine(ev.FirearmItem))
                {
                    if (!state.VirtualMagazines.TryGetValue(ev.FirearmItem.Serial, out VirtualMagazine magazine))
                        state.VirtualMagazines[ev.FirearmItem.Serial] = magazine = new VirtualMagazine();
                    magazine.Reload();
                }
                if (ev.FirearmItem.AmmoType != ItemType.None) ev.Player.SetAmmo(ev.FirearmItem.AmmoType, 67);
            }
        }
    }

    private void OnShotWeapon(PlayerShotWeaponEventArgs ev)
    {
        if (buffs.TryGetValue(ev.Player.PlayerId, out BuffState state) && state.Drink == Drink.SixtySeven &&
            state.EndAt > Time.realtimeSinceStartup &&
            state.VirtualMagazines.TryGetValue(ev.FirearmItem.Serial, out VirtualMagazine magazine))
            magazine.Shot();
    }

    private void CommitVirtualRefills()
    {
        foreach (var pair in buffs)
        {
            BuffState state = pair.Value;
            if (state.Drink != Drink.SixtySeven || state.EndAt <= Time.realtimeSinceStartup) continue;
            Player? player = Player.Get(pair.Key);
            if (player == null || !player.IsAlive || player.LifeId != state.LifeId) continue;
            foreach (FirearmItem firearm in player.Items.OfType<FirearmItem>())
            {
                if (!state.VirtualMagazines.TryGetValue(firearm.Serial, out VirtualMagazine magazine) || !magazine.RefillPending) continue;
                if (firearm.Base.Modules.OfType<InventorySystem.Items.Firearms.Modules.IReloaderModule>().Any(m => m.IsReloadingOrUnloading)) continue;
                WeaponAmmo67.RefillVirtualMagazine(firearm, magazine.Remaining);
                magazine.RefillCommitted();
            }
        }
    }

    private void OnHurting(PlayerHurtingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (buffs.TryGetValue(ev.Player.PlayerId, out BuffState victim) && victim.Drink == Drink.Jiahao &&
            victim.EndAt > Time.realtimeSinceStartup && ev.Player.LifeId == victim.LifeId &&
            ev.DamageHandler is StandardDamageHandler damage && damage.Damage != 0 &&
            (damage is FirearmDamageHandler || ev.Attacker?.IsSCP == true || damage is ScpDamageHandler))
        {
            // This LabAPI build ignores replacement handlers and applies the original.
            // Mutate it in place and neutralize headshot/effect multipliers before native
            // armor processing, which can only further reduce the one-point attack.
            damage.Hitbox = HitboxType.Body;
            float multiplier = 1f;
            foreach (var effect in ev.Player.ReferenceHub.playerEffectsController.AllEffects)
                if (effect is CustomPlayerEffects.IDamageModifierEffect modifier && modifier.DamageModifierActive)
                    multiplier *= modifier.GetDamageModifier(1f, damage, HitboxType.Body);
            damage.Damage = multiplier > 0f ? 1f / multiplier : 0f;
            if (ev.Attacker != null)
                Notice(ev.Attacker, "<color=#D8C8F0>雕虫小记，不可伤</color><color=#FFD700><b>神</b></color><color=#D8C8F0>分毫</color>", 3f);
        }
        if (ev.Attacker == null || !buffs.TryGetValue(ev.Attacker.PlayerId, out BuffState state) || state.Drink != Drink.Ahead ||
            state.EndAt <= Time.realtimeSinceStartup || ev.DamageHandler is not FirearmDamageHandler firearm)
            return;
        firearm.Damage *= 1.10f;
    }

    private void OnUpdatingEffect(PlayerEffectUpdatingEventArgs ev)
    {
        if (ev.Intensity == 0 || !buffs.TryGetValue(ev.Player.PlayerId, out BuffState state) || state.Restoring || state.Drink != Drink.Jiahao ||
            state.EndAt <= Time.realtimeSinceStartup || ev.Player.LifeId != state.LifeId) return;
        if (ev.Effect is CustomPlayerEffects.Scp1853) ev.Intensity = 3;
        // Native 1853 + 207 creates poison. Protect this deliberately combined drink from
        // that automatic conflict, without deleting poison the player already had.
        if (ev.Effect is CustomPlayerEffects.Poisoned) ev.IsAllowed = false;
    }

    private void ExplodeMeteor(Player player, BuffState state)
    {
        Vector3 position = player.Position;
        RestoreBase(player, state);
        buffs.Remove(player.PlayerId);
        ClearBuffHud(player);
        TimedGrenadeProjectile? grenade = TimedGrenadeProjectile.SpawnActive(position, ItemType.GrenadeHE, player, 0);
        if (grenade != null) grenade.FuseEnd();
        else TimedGrenadeProjectile.PlayEffect(position, ItemType.GrenadeHE);
        if (player.IsAlive) player.Kill("美味流星：燃尽了，化作一场自爆");
    }

    private IEnumerator<float> Tick()
    {
        float nextHudUpdate = 0f;
        while (true)
        {
            float now = Time.realtimeSinceStartup;
            CommitVirtualRefills();
            if (now < nextHudUpdate) { yield return Timing.WaitForOneFrame; continue; }
            nextHudUpdate = now + 0.1f;
            Hints.Tick(now);
            if (now >= spawnAt)
            {
                spawnAt = float.PositiveInfinity;
                if (Round.IsRoundInProgress) SpawnMachine();
            }
            foreach (Player player in Player.ReadyList)
            {
                if (!player.IsAlive) { ClearItemHud(player); ClearBuffHud(player); continue; }
                UpdateItemHud(player);
                if (!buffs.ContainsKey(player.PlayerId)) ClearBuffHud(player);
            }
            foreach (var pair in buffs.ToArray())
            {
                Player? player = Player.Get(pair.Key);
                BuffState state = pair.Value;
                if (player == null || !player.IsAlive || player.LifeId != state.LifeId)
                {
                    state.Music?.Stop();
                    if (player != null) Hints.Clear(player);
                    buffs.Remove(pair.Key);
                    continue;
                }
                if (state.Drink == Drink.SixtySeven && state.EndAt > now)
                    SetAmmoTo67(player, state);
                state.Music?.Follow(player);
                if (state.Drink == Drink.CompoundV && state.Timeline!.TryStartBacklash(now, Config!.CompoundVBacklashDuration))
                {
                    state.EndAt = state.Timeline.EndAt;
                    player.DisableEffect<CustomPlayerEffects.MovementBoost>();
                    float left = Math.Max(0.01f, state.EndAt - now);
                    player.EnableEffect<CustomPlayerEffects.Burned>(1, left, false);
                    player.EnableEffect<CustomPlayerEffects.Slowness>(Config.CompoundVBacklashSlowness, left, false);
                    player.EnableEffect<CustomPlayerEffects.Concussed>(1, left, false);
                    Notice(player, "<color=#FF7777>体验卡到期，5号化合物开始反噬了！</color>");
                }
                if (state.Drink == Drink.Meteor && (state.EndAt <= now || state.Motion!.ShouldExplode(now, player.Velocity.magnitude, Config!.MeteorStillSeconds)))
                {
                    ExplodeMeteor(player, state);
                    continue;
                }
                if (state.Drink == Drink.Ahead && !state.AheadDebuffApplied && state.EndAt <= now)
                {
                    player.DisableEffect<CustomPlayerEffects.MovementBoost>();
                    player.EnableEffect<CustomPlayerEffects.Slowness>(50, 999999f, false);
                    player.MaxHealth = Math.Max(1f, state.BaseMaxHealth * 0.5f);
                    player.Health = Math.Min(player.Health, player.MaxHealth);
                    state.AheadDebuffApplied = true;
                    ClearBuffHud(player);
                    Notice(player, "<color=#FF7777>你被GOC卡脖子了（悲</color>");
                    // Keep state for cleanup/replacement. The penalty has no expiry specified;
                    // it lasts for this life, and is labelled separately from a timed buff.
                }
                if (state.Drink != Drink.Ahead && state.EndAt <= now)
                {
                    RestoreBase(player, state);
                    buffs.Remove(pair.Key);
                    ClearBuffHud(player);
                    Notice(player, $"<color=#CCCCCC>{DrinkName(state.Drink)}效果已结束</color>", 3f);
                }
                else UpdateBuffHud(player, state, now);
            }
            yield return Timing.WaitForOneFrame;
        }
    }

    private static void RestoreBase(Player player, BuffState state)
    {
        state.Restoring = true;
        state.Music?.Stop();
        if (state.Ahp != null)
        {
            try { player.ServerKillProcess(state.Ahp); } catch { }
            state.Ahp = null;
        }
        player.Scale = state.BaseScale;
        if (state.Drink != Drink.Ahead || state.AheadDebuffApplied)
        {
            player.MaxHealth = state.BaseMaxHealth;
            // Preserve damage taken; returning to a captured HP value would heal on expiry.
            player.Health = Math.Min(player.Health, player.MaxHealth);
        }
        if (state.Drink == Drink.SixtySeven)
        {
            player.MaxHumeShield = state.BaseMaxHume;
            player.HumeShield = Math.Min(state.BaseHume, player.MaxHumeShield);
            foreach (FirearmItem firearm in player.Items.OfType<FirearmItem>())
                if (state.ModifiedWeapons.Contains(firearm.Serial))
                    firearm.StoredAmmo = Math.Min(firearm.StoredAmmo, firearm.MaxAmmo);
        }
        foreach (var pair in state.Effects)
        {
            bool modified = state.Drink == Drink.Vodka && (pair.Key is CustomPlayerEffects.Slowness || pair.Key is CustomPlayerEffects.DamageReduction)
                || state.Drink == Drink.Ahead && (pair.Key is CustomPlayerEffects.MovementBoost || state.AheadDebuffApplied && pair.Key is CustomPlayerEffects.Slowness)
                || state.Drink == Drink.QiaoLeZi && (state.QiaoLeZiCardiac ? pair.Key is CustomPlayerEffects.CardiacArrest : pair.Key is CustomPlayerEffects.MovementBoost)
                || state.Drink == Drink.CompoundV && (pair.Key is CustomPlayerEffects.MovementBoost || state.Timeline!.Backlash &&
                    (pair.Key is CustomPlayerEffects.Burned || pair.Key is CustomPlayerEffects.Slowness || pair.Key is CustomPlayerEffects.Concussed))
                || state.Drink == Drink.Meteor && (pair.Key is CustomPlayerEffects.MovementBoost || pair.Key is CustomPlayerEffects.Scp207)
                || state.Drink == Drink.Jiahao && (pair.Key is CustomPlayerEffects.Scp1853 || pair.Key is CustomPlayerEffects.Scp207 || pair.Key is CustomPlayerEffects.Poisoned);
            if (!modified) continue;
            float left = pair.Value.EndAt - Time.realtimeSinceStartup;
            pair.Key.ServerSetState(left > 0 ? pair.Value.Intensity : (byte)0, float.IsPositiveInfinity(left) ? 0f : Math.Max(0f, left));
        }
    }

    private static string DrinkName(Drink drink) => drink switch
    {
        Drink.QiaoLeZi => "巧乐兹", Drink.SixtySeven => "67", Drink.Ahead => "遥遥领先", Drink.Vodka => "伏特加",
        Drink.CompoundV => "5号化合物", Drink.Meteor => "美味流星",
        Drink.Jiahao => "嘉豪の圣遗物？",
        _ => throw new ArgumentOutOfRangeException(nameof(drink)),
    };
    private static string DrinkIntro(Drink drink) => drink switch
    {
        Drink.QiaoLeZi => "我看你嘴唇发紫是不是心脏不好",
        Drink.SixtySeven => "676767？", Drink.Ahead => "让 GOC 永远追不上！", Drink.Vodka => "某神秘民族的白开水？",
        Drink.CompoundV => "沃特出品，英雄也有保质期。", Drink.Meteor => "把自己变成夜空中最美味的那颗星。",
        Drink.Jiahao => "凡人的身躯，能驾驭这份力量吗？",
        _ => throw new ArgumentOutOfRangeException(nameof(drink)),
    };
    private void Notice(Player player, string text, float? duration = null) =>
        Hints.Show(player, "message", text, Config!.MessageHintY, 23, Math.Max(0.5f, duration ?? Config.HintMessageDuration));

    private void UpdateItemHud(Player player)
    {
        Item? item = player.CurrentItem;
        if (item == null || !drinks.TryGetValue(item.Serial, out Drink drink)) { ClearItemHud(player); return; }
        float y = Config!.ItemHintY;
        Hints.Show(player, "item-name", $"<color=#FFD447>{DrinkName(drink)}</color>", y, 26, 0f);
        Hints.Show(player, "item-intro", $"<color=#C8D3E0>{DrinkIntro(drink)}</color>", y + 38f, 22, 0f);
    }
    private static void ClearItemHud(Player player)
    {
        Hints.Remove(player, "item-name"); Hints.Remove(player, "item-intro"); Hints.Remove(player, "item-detail");
    }
    private void UpdateBuffHud(Player player, BuffState state, float now)
    {
        float y = Config!.BuffHintY;
        string label = state.AheadDebuffApplied ? $"{DrinkName(state.Drink)}[本条生命]" : BuffLabel.Timed(DrinkName(state.Drink), state.EndAt - now);
        Hints.Remove(player, "buff-name");
        Hints.Show(player, "buff-time", $"<color=#FFD447>{label}</color>", y, 23, 0f, Config.BuffHintX, "Right");
        if (player.CurrentItem is FirearmItem firearm && state.VirtualMagazines.TryGetValue(firearm.Serial, out VirtualMagazine magazine))
            Hints.Show(player, "buff-ammo", $"<color=#FFD447>67 弹匣：{magazine.Remaining} / 67</color>", y + 38f, 22, 0f, Config.BuffHintX, "Right");
        else Hints.Remove(player, "buff-ammo");
    }
    private static void ClearBuffHud(Player player)
    {
        Hints.Remove(player, "buff-name"); Hints.Remove(player, "buff-time"); Hints.Remove(player, "buff-detail");
        Hints.Remove(player, "buff-ammo");
    }

    private void OnLeft(PlayerLeftEventArgs ev)
    {
        if (buffs.TryGetValue(ev.Player.PlayerId, out BuffState state)) state.Music?.Stop();
        Hints.Clear(ev.Player); buffs.Remove(ev.Player.PlayerId); nextUse.Remove(ev.Player.PlayerId);
        bottleQuota.Forget(ev.Player.PlayerId);
    }
    private void OnServerRoleSet(ReferenceHub hub, RoleTypeId newRole, RoleChangeReason reason)
    {
        Player? player = Player.Get(hub);
        if (player != null && buffs.TryGetValue(player.PlayerId, out BuffState state) &&
            state.Drink == Drink.Vodka && player.LifeId == state.LifeId)
        {
            // Native role changes retain FPC scale. This event runs after cancellation
            // checks and before replacing the old role, while its scale is still writable.
            player.Scale = state.BaseScale;
        }
    }

    private void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (buffs.TryGetValue(ev.Player.PlayerId, out BuffState state))
        {
            if (state.Drink == Drink.Jiahao && ev.ChangeReason == RoleChangeReason.Died && state.EndAt > Time.realtimeSinceStartup)
                GlobalNotice($"<color=#D8C8F0><i>真是意犹未尽呐~</i></color>\n<color=#FFD700><b>{EscapeName(state.HolderName)}老师</b></color><color=#DCE5EE>，我可能这辈子也不会忘记你吧</color>", 5f);
            state.Music?.Stop();
        }
        ClearItemHud(ev.Player); ClearBuffHud(ev.Player); buffs.Remove(ev.Player.PlayerId);
    }
    private static string EscapeName(string name) => name.Replace("<", "＜").Replace(">", "＞");
    private void OnDeath(PlayerDeathEventArgs ev)
    {
        if (buffs.TryGetValue(ev.Player.PlayerId, out BuffState state) && ev.Player.LifeId == state.LifeId)
            RestoreBase(ev.Player, state);
        buffs.Remove(ev.Player.PlayerId);
        ClearItemHud(ev.Player); ClearBuffHud(ev.Player);
    }
}
