using System;
using CommandSystem;

namespace Qlz;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class QlzCommand : ParentCommand
{
    public override string Command => "scp294";
    public override string[] Aliases => new[] { "qlz", "big207", "drink207" };
    public override string Description => "控制 SCP-294? 搞怪饮料机。";

    public QlzCommand() => LoadGeneratedCommands();

    public override void LoadGeneratedCommands()
    {
        RegisterCommand(new Spawn());
        RegisterCommand(new Clear());
        RegisterCommand(new BuffCommand());
    }

    protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = "用法：scp294 spawn | clear | buff（give/status/clear/list）";
        return false;
    }

    internal static bool Allowed(ICommandSender sender, out string response)
    {
        if (sender.CheckPermission(PlayerPermissions.ServerConfigs, out _))
        {
            response = string.Empty;
            return true;
        }
        response = "需要 ServerConfigs 权限。";
        return false;
    }

    private sealed class Spawn : ICommand
    {
        public string Command => "spawn";
        public string[] Aliases => new[] { "create", "rearm" };
        public string Description => "生成或重置 SCP-294 饮料机。";
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Allowed(sender, out response)) return false;
            response = QlzPlugin.Instance?.SpawnCommand() ?? "SCP-294? 未加载。";
            return true;
        }
    }

    private sealed class Clear : ICommand
    {
        public string Command => "clear";
        public string[] Aliases => new[] { "remove", "stop" };
        public string Description => "清除 SCP-294 饮料机。";
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Allowed(sender, out response)) return false;
            response = QlzPlugin.Instance?.ClearCommand() ?? "SCP-294? 未加载。";
            return true;
        }
    }
}
