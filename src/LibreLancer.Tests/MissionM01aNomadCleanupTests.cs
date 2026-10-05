using System;
using System.IO;
using System.Linq;
using LibreLancer.Data.Schema.Missions;
using Xunit;

namespace LibreLancer.Tests;

public class MissionM01aNomadCleanupTests
{
    [Fact]
    public void CloakedFp7AttackersAreDestroyedAfterCloakAnimation()
    {
        var path = FindRepositoryFile("data/MISSIONS/M01A/m01a.ini");
        var ini = new MissionIni();
        using (var stream = File.OpenRead(path))
            ini.ParseIni(stream, path);

        var trigger = Assert.Single(ini.Triggers.Where(x => x.Nickname == "fp7_attacker_cleanup"));
        var destroyActions = trigger.Actions.Where(x => x.Type == TriggerActions.Act_Destroy).ToArray();
        Assert.Equal(3, destroyActions.Length);
        Assert.Equal(new[] { "fp7base_attacker1", "fp7base_attacker2", "fp7base_attacker3" },
            destroyActions.Select(x => x.Entry[0].ToString()).ToArray());
        Assert.All(destroyActions, x => Assert.Equal("SILENT", x.Entry[1].ToString()));
        var shipNicknames = ini.Ships.Select(x => x.Nickname).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        Assert.All(destroyActions, x => Assert.Contains(x.Entry[0].ToString(), shipNicknames));

        Assert.Contains(trigger.Conditions, x => x.Type == TriggerConditions.Cnd_Timer && x.Entry[0].ToSingle() == 5f);
    }

    [Fact]
    public void OrderFightersKeepAvoidanceEnabledWhileApproachingTheDonau()
    {
        var path = FindRepositoryFile("data/MISSIONS/M01A/m01a.ini");
        var ini = new MissionIni();
        using (var stream = File.OpenRead(path))
            ini.ParseIni(stream, path);

        foreach (var nickname in new[]
                 { "ol_order_attack", "ol_order_attack_the_donau", "ol_order_attack_donau_slower" })
        {
            var order = Assert.Single(ini.ObjLists.Where(x => x.Nickname == nickname));
            var avoidanceCommands = order.Commands.Where(x => x.Command == ObjListCommands.Avoidance).ToArray();
            Assert.NotEmpty(avoidanceCommands);
            Assert.All(avoidanceCommands, x => Assert.True(x.Entry![0].ToBoolean()));
        }
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException($"Could not find {relativePath} above test output directory");
    }
}
