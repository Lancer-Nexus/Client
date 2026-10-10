using LancerEdit.GameContent.MissionEditor;
using LancerEdit.GameContent.MissionEditor.NodeTypes;
using LancerEdit.GameContent.MissionEditor.NodeTypes.Conditions;
using LibreLancer.Data.Schema.Missions;
using Xunit;

namespace LibreLancer.Tests;

public class MissionTriggerSaveValidationTests
{
    [Fact]
    public void EmptyWatchTriggerReferenceIsReportedBeforeSaving()
    {
        var trigger = new NodeMissionTrigger(new MissionTrigger { Nickname = "start" }, null);
        trigger.Conditions.Add(new CndWatchNodeTrigger(null));

        var errors = MissionScriptEditorTab.FindEmptyTriggerFields([trigger]);

        Assert.Equal(new[] { "'start' condition 'Cnd_WatchTrigger' argument 1" }, errors);
    }

    [Fact]
    public void NumericTriggerFieldsAreNotReportedAsEmpty()
    {
        var trigger = new NodeMissionTrigger(new MissionTrigger { Nickname = "start" }, null);
        trigger.Conditions.Add(new CndTimer(null));

        var errors = MissionScriptEditorTab.FindEmptyTriggerFields([trigger]);

        Assert.Empty(errors);
    }
}
