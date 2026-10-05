using System;
using LancerNexus.Protocol;

namespace LibreLancer.Server;

public sealed record GamePermissionMutationRequest(Guid AccountId, Guid SessionId, long CharacterId,
    Guid TransferId, PermissionCommandRequest Request);
public sealed record PermissionCommandRequest(Guid CorrelationId, string IdempotencyKey,
    PermissionCommandMutation Mutation);
public sealed record PermissionCommandMutation(int Kind, Guid? AccountId = null, string? GroupName = null,
    string? Ladder = null, string? Pattern = null, int PatternKind = 1, bool Allowed = true,
    string? InstanceId = null);
public sealed record PermissionCommandResponse(string Status, long? Revision);
public sealed record GamePermissionCheckRequest(Guid AccountId, Guid SessionId, long CharacterId,
    Guid TransferId, string Permission, string? SystemId = null);
public sealed record GamePermissionCheckResponse(bool Allowed);

internal static class PermissionChatCommand
{
    public static bool IsCommand(string text)
    {
        var trimmed = text.TrimStart();
        return IsSubcommand(trimmed, "/admin permission") || IsSubcommand(trimmed, "/admin op") ||
               IsSubcommand(trimmed, "/admin deop");
    }

    private static bool IsSubcommand(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        (text.Length == prefix.Length || char.IsWhiteSpace(text[prefix.Length]));

    public static bool TryParse(string text, string? instanceId, out PermissionCommandRequest? request)
    {
        request = null;
        if (text.Length > 512 || !IsCommand(text)) return false;
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 3 or 4 &&
            (parts[1].Equals("op", StringComparison.OrdinalIgnoreCase) ||
             parts[1].Equals("deop", StringComparison.OrdinalIgnoreCase)))
        {
            if (!Guid.TryParse(parts[2], out var target) || target == Guid.Empty ||
                (parts.Length == 4 && !ValidScope(parts[3]))) return false;
            var assign = parts[1].Equals("op", StringComparison.OrdinalIgnoreCase);
            var operatorMutation = new PermissionCommandMutation(assign ? 2 : 3, AccountId: target,
                GroupName: "operator", InstanceId: Scope(parts, 3, instanceId));
            var operatorId = Guid.NewGuid();
            request = new PermissionCommandRequest(operatorId, operatorId.ToString("N"), operatorMutation);
            return true;
        }
        if (parts.Length < 5) return false;
        PermissionCommandMutation mutation;
        if (parts.Length is 7 or 8 && parts[2].Equals("node", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(parts[3], out var accountId) || accountId == Guid.Empty) return false;
            var allowed = parts[4].ToLowerInvariant() switch { "allow" => true, "deny" => false, _ => (bool?)null };
            var kind = parts[5].ToLowerInvariant() switch { "exact" => 0, "wildcard" => 1, "regex" => 2, _ => -1 };
            if (allowed is null || kind < 0 || parts[6].Length is 0 or > 256 ||
                (parts.Length == 8 && !ValidScope(parts[7]))) return false;
            mutation = new PermissionCommandMutation(4, AccountId: accountId, Pattern: parts[6], PatternKind: kind,
                Allowed: allowed.Value, InstanceId: Scope(parts, 7, instanceId));
        }
        else if (parts.Length is 6 or 7 && parts[2].Equals("group", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(parts[4], out var accountId) || accountId == Guid.Empty ||
                (parts.Length == 7 && !ValidScope(parts[6]))) return false;
            if (parts[5].Length is 0 or > 64) return false;
            var scope = Scope(parts, 6, instanceId);
            switch (parts[3].ToLowerInvariant())
            {
                case "assign": mutation = new PermissionCommandMutation(2, AccountId: accountId, GroupName: parts[5], InstanceId: scope); break;
                case "remove": mutation = new PermissionCommandMutation(3, AccountId: accountId, GroupName: parts[5], InstanceId: scope); break;
                case "promote": mutation = new PermissionCommandMutation(6, AccountId: accountId, Ladder: parts[5], InstanceId: scope); break;
                case "demote": mutation = new PermissionCommandMutation(7, AccountId: accountId, Ladder: parts[5], InstanceId: scope); break;
                default: return false;
            }
        }
        else return false;
        var id = Guid.NewGuid();
        request = new PermissionCommandRequest(id, id.ToString("N"), mutation);
        return true;
    }

    private static string? Scope(string[] parts, int index, string? instanceId)
    {
        if (parts.Length <= index) return instanceId;
        if (parts[index].Equals("global", StringComparison.OrdinalIgnoreCase)) return null;
        return parts[index].Equals("instance", StringComparison.OrdinalIgnoreCase) ? instanceId : null;
    }

    private static bool ValidScope(string value) => value.Equals("global", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("instance", StringComparison.OrdinalIgnoreCase);
}
