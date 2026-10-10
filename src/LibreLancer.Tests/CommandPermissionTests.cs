using System;
using System.Linq;
using LibreLancer.Server;
using LibreLancer.Server.ConsoleCommands;
using Xunit;

namespace LibreLancer.Tests;

public sealed class CommandPermissionTests
{
    private static readonly Guid Target = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Theory]
    [InlineData("addcash", "command.addcash")]
    [InlineData("base", "command.base")]
    [InlineData("damageself", "command.damageself")]
    [InlineData("godmode", "command.godmode")]
    [InlineData("npc", "command.npc")]
    [InlineData("setlevel", "command.setlevel")]
    [InlineData("spawnloot", "command.spawnloot")]
    [InlineData("stop", "command.stop")]
    [InlineData("warp", "command.warp")]
    public void AdminCommandsDeclareTheirOwnPermission(string name, string permission)
    {
        var command = ConsoleCommands.AllCommands.Single(x => x.Name == name);
        Assert.Equal(permission, command.Permission);
    }

    [Theory]
    [InlineData("/admin op 22222222-2222-4222-8222-222222222222 global", 2, null)]
    [InlineData("/admin op 22222222-2222-4222-8222-222222222222", 2, "li-01")]
    [InlineData("/admin deop 22222222-2222-4222-8222-222222222222 instance", 3, "li-01")]
    [InlineData("/admin deop 22222222-2222-4222-8222-222222222222 global", 3, null)]
    public void OperatorAliasesMapToScopedOperatorMembership(string command, int kind, string? instance)
    {
        Assert.True(PermissionChatCommand.IsCommand(command));
        Assert.True(PermissionChatCommand.TryParse(command, "li-01", out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(kind, parsed.Mutation.Kind);
        Assert.Equal(Target, parsed.Mutation.AccountId);
        Assert.Equal("operator", parsed.Mutation.GroupName);
        Assert.Equal(instance, parsed.Mutation.InstanceId);
    }

    [Theory]
    [InlineData("/admin op not-a-uuid")]
    [InlineData("/admin deop 00000000-0000-0000-0000-000000000000")]
    [InlineData("/admin op 22222222-2222-4222-8222-222222222222 remote")]
    public void InvalidOperatorAliasesAreRejected(string command) =>
        Assert.False(PermissionChatCommand.TryParse(command, "li-01", out _));
}
