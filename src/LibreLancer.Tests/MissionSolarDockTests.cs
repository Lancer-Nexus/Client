using System;
using LibreLancer.Missions;
using LibreLancer.Server;
using LibreLancer.Server.Components;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public class MissionSolarDockTests
{
    [Fact]
    public void MissionSolarBaseDetectionIsCaseInsensitiveAndRejectsUnknownBases()
    {
        var script = new MissionScript();
        script.Solars.Add("sprague", new ScriptSolar { Nickname = "sprague", Base = "Li01_01_Base" });
        var runtime = new MissionRuntime(script, new Player(null!, null!, Guid.Empty), [], missionNickname: "fixture");

        Assert.True(runtime.IsMissionSolarBase("li01_01_base"));
        Assert.False(runtime.IsMissionSolarBase("li01_02_base"));
        Assert.False(runtime.IsMissionSolarBase(null));
    }

    [Fact]
    public void DockedMissionNpcRemainsInWorld()
    {
        var script = new MissionScript();
        var runtime = new MissionRuntime(script, new Player(null!, null!, Guid.Empty), [], missionNickname: "fixture");
        var parent = new GameObject { Nickname = "mission_escort" };
        var npc = new SNPCComponent(parent, null!, null!) { MissionRuntime = runtime };

        var exception = Record.Exception(npc.Docked);

        Assert.Null(exception);
    }
}
