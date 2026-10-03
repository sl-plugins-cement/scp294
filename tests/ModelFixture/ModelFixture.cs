using System;
using System.Linq;
using CommandSystem;
using LabApi.Features;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using PlayerRoles;
using UnityEngine;

namespace Scp294ModelFixture;

// Candidate-only arrangement and read-only inspection; native client input dispenses drinks.
public sealed class FixturePlugin : Plugin
{
    public override string Name => "SCP294ModelFixture";
    public override string Description => "Offline SCP-294 model fixture.";
    public override string Author => "Codex";
    public override Version Version => new(1, 0, 0);
    public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);
    public override void Enable() { }
    public override void Disable() { }
}

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class FixtureCommand : ICommand
{
    private static AdminToys.AdminToyBase[] observedToys = Array.Empty<AdminToys.AdminToyBase>();
    public string Command => "scp294fixture";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Offline-only model arrangement and inspection.";
    public bool Execute(ArraySegment<string> args, ICommandSender sender, out string response)
    {
        if (Environment.GetEnvironmentVariable("OFFLINE_LAB_OBSERVER") != "1" ||
            !sender.CheckPermission(PlayerPermissions.ServerConfigs, out _))
        {
            response = "Offline observer and ServerConfigs permission required.";
            return false;
        }
        var roots = PrimitiveObjectToy.List.Where(t => !t.IsDestroyed && t.GameObject.name == "SCP294-CoffeeMachine").ToArray();
        if (args.Count == 0 || args.At(0) == "state")
        {
            if (roots.Length != 1)
            {
                response = "MODEL_STATE {\"roots\":" + roots.Length + ",\"survivingToys\":" + observedToys.Count(t => t != null) + "}";
                return true;
            }
            var root = roots[0];
            observedToys = root.GameObject.GetComponentsInChildren<AdminToys.AdminToyBase>();
            var target = root.GameObject.GetComponentInChildren<AdminToys.InvisibleInteractableToy>();
            Vector3 point = target.transform.position;
            Vector3 basePos = root.Transform.position;
            response = FormattableString.Invariant($"MODEL_STATE {{\"roots\":1,\"toys\":{observedToys.Length},\"x\":{point.x},\"y\":{point.y},\"z\":{point.z},\"baseY\":{basePos.y}}}");
            return true;
        }
        if (args.Count != 2 || !int.TryParse(args.At(1), out int id) || !Player.TryGet(id, out Player player))
        { response = "Usage: scp294fixture place|fill|empty <id>"; return false; }
        switch (args.At(0))
        {
            case "place":
                if (roots.Length != 1) { response = "Expected one machine."; return false; }
                player.SetRole(RoleTypeId.Tutorial);
                player.IsGodModeEnabled = true;
                player.ClearInventory();
                player.Position = roots[0].Transform.TransformPoint(new Vector3(0, 1.04f, -1.65f));
                break;
            case "fill":
                player.ClearInventory();
                for (int i = 0; i < 8; i++) player.AddItem(ItemType.Flashlight);
                break;
            case "empty": player.ClearInventory(); break;
            default: response = "Unknown fixture operation."; return false;
        }
        response = "FIXTURE_OK " + args.At(0);
        return true;
    }
}
