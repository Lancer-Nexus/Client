using System;
using System.IO;
using LibreLancer.Data.IO;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class TransferLeaseTokenTests
{
    [Fact]
    public void TargetTokenUsesGatewayBase64UrlFormatAndBindsTransferAndInstance()
    {
        var game = new GameServer(new FileSystem(new EmptyFileSystem()))
        {
            InstanceId = "li02",
            SystemId = "li03",
            LoginUrl = "https://gateway.example",
            TransferInstanceKey = new string('k', 32)
        };
        var id = Guid.NewGuid();
        var token = game.CreateTargetTransferLeaseToken(id);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.Equal(32, Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/') + "=").Length);
        Assert.Equal(token, game.CreateTargetTransferLeaseToken(id));
        Assert.NotEqual(token, game.CreateTargetTransferLeaseToken(Guid.NewGuid()));
        game.InstanceId = "li04";
        Assert.NotEqual(token, game.CreateTargetTransferLeaseToken(id));
        Assert.Throws<InvalidOperationException>(() => game.CreateTargetTransferLeaseToken(Guid.Empty));
    }

    [Fact]
    public void GroupOwnershipKeepsInternalJumpsLocalAndRequiresTransfersOutsideTheGroup()
    {
        var game = new GameServer(new FileSystem(new EmptyFileSystem())) { SystemId = "li01" };
        Assert.True(game.OwnsSystem("LI01"));
        Assert.False(game.OwnsSystem("li03"));
        game.SystemIds = ["li01", "li02", "li03", "li04", "li05"];
        Assert.True(game.OwnsSystem("Li03"));
        Assert.False(game.OwnsSystem("br01"));
        Assert.False(game.OwnsSystem("li06"));
    }

    private sealed class EmptyFileSystem : BaseFileSystemProvider
    {
        public EmptyFileSystem() => Refresh();
        public override void Refresh()
        {
            Root = new VfsDirectory();
            var exe = new VfsDirectory { Name = "EXE", Parent = Root };
            Root.Items["EXE"] = exe;
            exe.Items.Add("freelancer.ini", new EmptyFile { Name = "freelancer.ini" });
        }
        private sealed class EmptyFile : VfsFile
        {
            public override Stream OpenRead() => new MemoryStream();
        }
    }
}
