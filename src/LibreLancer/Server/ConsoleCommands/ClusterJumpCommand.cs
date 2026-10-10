using System;
using System.Linq;
using LibreLancer.Data.GameData.World;

namespace LibreLancer.Server.ConsoleCommands;

[ConsoleCommand]
public sealed class ClusterJumpCommand : IConsoleCommand
{
    public string Name => "clusterjump";
    public string? Permission => "command.clusterjump";

    public void Run(Player player, string arguments)
    {
        if (!ConsoleCommands.ParseString<string, string>(arguments, out var targetSystemName, out var sourceGateName))
        {
            player.RpcClient.OnConsoleMessage("Usage: /clusterjump <system> <source gate>");
            return;
        }

        if (!player.Game.ClusterTransfersEnabled || player.Character is null || player.StableCharacterId <= 0 ||
            player.TransferInProgress)
        {
            player.RpcClient.OnConsoleMessage("Cluster jump is unavailable for this player or instance.");
            return;
        }

        var sourceSystem = player.Game.GameData.Items.Systems.Get(player.System);
        var targetSystem = player.Game.GameData.Items.Systems.Get(targetSystemName);
        if (sourceSystem is null || targetSystem is null || player.Game.OwnsSystem(targetSystem.Nickname))
        {
            player.RpcClient.OnConsoleMessage("Target system must be a remote system in the current cluster.");
            return;
        }

        var sourceGate = sourceSystem.Objects.FirstOrDefault(obj =>
            obj.Nickname.Equals(sourceGateName, StringComparison.OrdinalIgnoreCase));
        if (sourceGate?.Dock is not { Kind: DockKinds.Jump, Target: { Length: > 0 } nextSystem,
                Exit: { Length: > 0 } arrivalGate } ||
            !string.Equals(nextSystem, targetSystem.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            player.RpcClient.OnConsoleMessage("The source gate does not lead to that target system.");
            return;
        }

        var targetGate = targetSystem.Objects.FirstOrDefault(obj =>
            obj.Nickname.Equals(arrivalGate, StringComparison.OrdinalIgnoreCase));
        if (targetGate?.Dock is not { Kind: DockKinds.Jump, Target: { Length: > 0 } returnSystem,
                Exit: { Length: > 0 } returnGate } ||
            !string.Equals(returnSystem, sourceSystem.Nickname, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(returnGate, sourceGate.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            player.RpcClient.OnConsoleMessage("The target gate does not lead back to the current system.");
            return;
        }

        player.JumpTo(targetSystem.Nickname, arrivalGate, []);
        player.RpcClient.OnConsoleMessage($"Cluster jump to {targetSystem.Nickname} requested via {sourceGate.Nickname}.");
    }
}
