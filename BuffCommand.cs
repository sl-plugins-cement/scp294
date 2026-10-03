using System;
using System.Collections.Generic;
using System.Linq;
using CommandSystem;
using LabApi.Features.Wrappers;

namespace Qlz;

internal sealed class BuffCommand : ICommand
{
    public string Command => "buff";
    public string[] Aliases => new[] { "buffs" };
    public string Description => "直接给予、查询或清除饮料 Buff。";
    private const string Usage = "scp294 buff list | give <玩家ID/me/all/昵称> <Buff> [秒数] [分支] | status <玩家> | clear <玩家>";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!QlzCommand.Allowed(sender, out response)) return false;
        if (arguments.Count == 0) { response = Usage; return false; }
        string action = arguments.ElementAt(0).ToLowerInvariant();
        if (action == "list" && arguments.Count == 1)
        {
            response = BuffGrant.Catalog + "\n默认巧乐兹加速、嘉豪成功；可指定 cardiac/fail/random。";
            return true;
        }
        bool give = action == "give" || action == "add" || action == "grant";
        bool clear = action == "clear" || action == "remove";
        bool status = action == "status" || action == "show";
        if ((!give && !clear && !status) || (give ? arguments.Count < 3 : arguments.Count != 2))
        { response = Usage; return false; }
        BuffGrant? grant = null;
        if (give && !BuffGrant.TryParse(arguments.ElementAt(2), arguments.Skip(3), out grant, out response)) return false;
        QlzPlugin? plugin = QlzPlugin.Instance;
        if (plugin == null || !plugin.BuffsAvailable) { response = "SCP-294? 未启用。"; return false; }
        if (!ResolveTargets(arguments.ElementAt(1), sender, give, out List<Player> targets, out response)) return false;
        var results = new List<string>();
        foreach (Player target in targets)
        {
            if (give) results.Add(plugin.GiveBuff(target, grant!));
            else if (clear) results.Add(plugin.ClearBuff(target));
            else results.Add(plugin.GetBuffStatus(target));
        }
        response = string.Join("\n", results);
        return true;
    }

    private static bool ResolveTargets(string selector, ICommandSender sender, bool give, out List<Player> targets, out string error)
    {
        targets = new List<Player>();
        error = string.Empty;
        if (selector.Equals("all", StringComparison.OrdinalIgnoreCase) || selector == "*")
            targets.AddRange(Player.ReadyList.Where(p => !give || p.IsAlive && p.IsHuman));
        else if (selector.Equals("me", StringComparison.OrdinalIgnoreCase))
        {
            Player? self = Player.Get(sender);
            if (self != null) targets.Add(self);
        }
        else if (int.TryParse(selector, out int id))
        {
            Player? target = Player.Get(id);
            if (target != null) targets.Add(target);
        }
        else
        {
            targets.AddRange(Player.ReadyList.Where(p => p.UserId.Equals(selector, StringComparison.OrdinalIgnoreCase) ||
                p.Nickname.Equals(selector, StringComparison.OrdinalIgnoreCase)));
            if (targets.Count == 0)
                targets.AddRange(Player.ReadyList.Where(p => p.Nickname.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0));
            if (targets.Count > 1)
            {
                error = "昵称匹配多个玩家，请使用 ID：" + string.Join("、", targets.Select(p => $"{p.Nickname}({p.PlayerId})"));
                return false;
            }
        }
        if (targets.Count == 0) { error = "未找到目标；服务器控制台不能使用 me。all 给予时仅选取存活人类。"; return false; }
        if (give && targets.Any(p => !p.IsAlive || !p.IsHuman)) { error = "只能向存活的人类角色给予饮料 Buff。"; return false; }
        return true;
    }
}
