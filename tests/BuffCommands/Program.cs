using System;
using System.Collections.Generic;
using System.Linq;
using CommandSystem;
using LabApi.Features.Wrappers;
using Qlz;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++;
    }

    public static void Main()
    {
        var admin = new Player(1, "管理员") { RemoteAdminAccess = true };
        var alice = new Player(2, "Alice") { UserId = "alice@steam" };
        var alicia = new Player(3, "Alicia");
        var scp = new Player(4, "SCP") { IsHuman = false };
        var observer = new Player(5, "观察者") { IsAlive = false };
        Player.Players.AddRange(new[] { admin, alice, alicia, scp, observer });
        var plugin = new QlzPlugin();
        QlzPlugin.Instance = plugin;
        var command = new BuffCommand();
        var sender = new Sender { Player = admin, Permission = true };
        bool Run(string[] args, out string response, ICommandSender? caller = null) =>
            command.Execute(new ArraySegment<string>(args), caller ?? sender, out response);

        Check(!Run(new[] { "give", "2", "67" }, out _, new Sender { Player = alice }) && plugin.GiveCount == 0,
            "普通玩家不能给予 Buff");
        Check(!Run(new[] { "give", "2", "67" }, out _, new Sender()) && plugin.GiveCount == 0,
            "无权限控制台拒绝");
        var sponsor = new Sender { Player = admin, Permission = false };
        Check(!Run(new[] { "give", "all", "jiahao", "fail" }, out _, sponsor) && plugin.GiveCount == 0,
            "RA access alone cannot grant the lethal buff to all players");
        Check(!Run(new[] { "clear", "all" }, out _, sponsor) && plugin.ClearCount == 0,
            "RA access alone cannot clear buffs");
        var parent = new QlzCommand();
        foreach (string action in new[] { "spawn", "clear" })
            Check(!parent.Children.Single(c => c.Command == action).Execute(default, sponsor, out _),
                "RA access alone cannot " + action + " the machine");
        Check(Run(new[] { "give", "me", "67" }, out _) && plugin.LastTarget == admin && plugin.LastGrant!.Drink == Drink.SixtySeven,
            "管理员自身直接给予");
        Check(Run(new[] { "give", "2", "coffee", "5" }, out _) && plugin.LastGrant!.Drink == Drink.Coffee && plugin.LastGrant.Duration == 5,
            "Coffee admin grant with a bounded duration");
        Check(Run(new[] { "give", "2", "普通咖啡" }, out _) && plugin.LastGrant!.Drink == Drink.Coffee,
            "Coffee's in-game name resolves to the same drink");
        Check(Run(new[] { "give", "2", "巧乐兹" }, out _) && plugin.LastGrant!.Branch == BuffBranch.Primary && plugin.LastGrant.Duration == null,
            "巧乐兹默认为加速，沿用配置时长");
        Check(Run(new[] { "give", "2", "qlz", "cardiac", "12.5" }, out _) && plugin.LastGrant!.Branch == BuffBranch.Secondary && plugin.LastGrant.Duration == 12.5f,
            "心脏骤停与自定义时间");
        Check(Run(new[] { "give", "2", "嘉豪の圣遗物？" }, out _) && plugin.LastGrant!.Drink == Drink.Jiahao && plugin.LastGrant.Branch == BuffBranch.Primary,
            "嘉豪默认成功");
        Check(Run(new[] { "give", "2", "jiahao", "fail" }, out _) && plugin.LastGrant!.Branch == BuffBranch.Secondary,
            "管理员可指定嘉豪失败");
        Check(Run(new[] { "give", "2", "jiahao", "random", "30" }, out _) && plugin.LastGrant!.Branch == BuffBranch.Random && plugin.LastGrant.Duration == 30,
            "明确指定随机分支");
        int before = plugin.GiveCount;
        foreach (string invalid in new[] { "0", "-1", "3601", "NaN", "Infinity", "1e50", "abc" })
            Check(!Run(new[] { "give", "2", "67", invalid }, out _) && plugin.GiveCount == before, "非法参数不产生副作用：" + invalid);
        Check(!Run(new[] { "give", "all", "67", "1", "2" }, out _) && plugin.GiveCount == before,
            "重复时长在全体修改前拒绝");
        Check(!Run(new[] { "give", "all", "vodka", "cardiac" }, out _) && plugin.GiveCount == before,
            "错误分支在全体修改前拒绝");
        Check(!Run(new[] { "give", "2", "jiahao", "success", "fail" }, out _) && plugin.GiveCount == before,
            "冲突分支拒绝");
        Check(!Run(new[] { "give", "2", "不存在" }, out _) && plugin.GiveCount == before,
            "未知 Buff 拒绝");
        Check(!Run(new[] { "give", "Ali", "67" }, out string ambiguous) && ambiguous.Contains("Alice(2)") && plugin.GiveCount == before,
            "歧义昵称列出 ID，不修改玩家");
        Check(Run(new[] { "give", "Alice", "vodka" }, out _) && plugin.LastTarget == alice,
            "优先匹配完整昵称");
        Check(Run(new[] { "give", "alice@steam", "meteor" }, out _) && plugin.LastTarget == alice,
            "匹配完整用户 ID");
        Check(Run(new[] { "give", "licia", "v" }, out _) && plugin.LastTarget == alicia,
            "唯一昵称片段");
        before = plugin.GiveCount;
        Check(!Run(new[] { "give", "4", "67" }, out _) && plugin.GiveCount == before, "不能给 SCP 直接套人类属性");
        Check(!Run(new[] { "give", "5", "67" }, out _) && plugin.GiveCount == before, "不能给观察者给予 Buff");
        Check(!Run(new[] { "give", "999", "67" }, out _) && plugin.GiveCount == before, "目标不存在");
        Check(!Run(new[] { "give", "me", "67" }, out _, new Sender { Permission = true }) && plugin.GiveCount == before,
            "控制台 me 不会指向随机玩家");
        Check(Run(new[] { "give", "2", "ahead" }, out _, new Sender { Permission = true }), "有权限服务器控制台可使用玩家 ID");
        before = plugin.GiveCount;
        Check(Run(new[] { "give", "all", "67" }, out _) && plugin.GiveCount == before + 3,
            "全体给予仅选取存活人类");
        Check(Run(new[] { "clear", "all" }, out _) && plugin.ClearCount == 5,
            "全体清除可以清理所有角色");
        Check(Run(new[] { "status", "me" }, out _) && plugin.StatusCount == 1, "状态查询");
        Check(!Run(new[] { "clear", "all", "extra" }, out _) && plugin.ClearCount == 5,
            "多余参数不会触发清除");
        Check(Run(new[] { "list" }, out string catalog) && catalog.Contains("67") && catalog.Contains("嘉豪"), "查询所有 Buff");
        plugin.BuffsAvailable = false;
        Check(!Run(new[] { "give", "2", "67" }, out _), "插件禁用时拒绝给予");
        QlzPlugin.Instance = null;
        Check(!Run(new[] { "give", "2", "67" }, out _), "插件未加载时拒绝给予");
        // Verify a nonzero ArraySegment offset, as used by native parent command dispatch.
        QlzPlugin.Instance = plugin;
        plugin.BuffsAvailable = true;
        Check(command.Execute(new ArraySegment<string>(new[] { "scp294", "buff", "give", "2", "67" }, 2, 3), sender, out _)
            && plugin.LastTarget == alice, "父命令截取参数偏移");
        Check(new QlzCommand().Children.Any(c => c is BuffCommand), "父命令已注册 Buff 指令");
        Console.WriteLine($"BuffCommands: {checks} checks passed.");
    }
}

namespace CommandSystem
{
    public enum PlayerPermissions { ServerConfigs }
    public interface ICommandSender { bool CheckPermission(PlayerPermissions permission, out string response); }
    public interface ICommand
    {
        string Command { get; }
        string[] Aliases { get; }
        string Description { get; }
        bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response);
    }
    public class RemoteAdminCommandHandler { }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CommandHandlerAttribute : Attribute { public CommandHandlerAttribute(Type handler) { } }
    public abstract class ParentCommand : ICommand
    {
        public List<ICommand> Children { get; } = new();
        public abstract string Command { get; }
        public abstract string[] Aliases { get; }
        public abstract string Description { get; }
        public abstract void LoadGeneratedCommands();
        protected void RegisterCommand(ICommand command) => Children.Add(command);
        protected abstract bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response);
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response) => ExecuteParent(arguments, sender, out response);
    }
    public sealed class Sender : ICommandSender
    {
        public Player? Player;
        public bool Permission;
        public bool CheckPermission(PlayerPermissions permission, out string response) { response = ""; return Permission; }
    }
}

namespace LabApi.Features.Wrappers
{
    public sealed class Player
    {
        public static readonly List<Player> Players = new();
        public static IEnumerable<Player> ReadyList => Players;
        public int PlayerId;
        public string Nickname;
        public string UserId = "";
        public bool IsAlive = true, IsHuman = true, RemoteAdminAccess;
        public Player(int id, string nickname) { PlayerId = id; Nickname = nickname; }
        public static Player? Get(int id) => Players.FirstOrDefault(p => p.PlayerId == id);
        public static Player? Get(ICommandSender sender) => (sender as Sender)?.Player;
    }
}

namespace Qlz
{
    internal sealed class QlzPlugin
    {
        public static QlzPlugin? Instance;
        public bool BuffsAvailable = true;
        public int GiveCount, ClearCount, StatusCount;
        public Player? LastTarget;
        public BuffGrant? LastGrant;
        public string GiveBuff(Player player, BuffGrant grant) { GiveCount++; LastTarget = player; LastGrant = grant; return "given"; }
        public string ClearBuff(Player player) { ClearCount++; return "cleared"; }
        public string GetBuffStatus(Player player) { StatusCount++; return "status"; }
        public string SpawnCommand() => "spawn";
        public string ClearCommand() => "clear";
    }
}
