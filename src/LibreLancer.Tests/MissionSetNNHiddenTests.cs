using LibreLancer.Data.Ini;
using LibreLancer.Data.Schema.Missions;
using LibreLancer.Missions.Actions;
using Xunit;

namespace LibreLancer.Tests;

public sealed class MissionSetNNHiddenTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ParsesObjectiveAndHideFlagFromSeparateArguments(string hideValue, bool expectedHide)
    {
        var entry = new Entry(new Section("Trigger"), "Act_SetNNHidden");
        entry.Add((ValueBase)"mlog_21855");
        entry.Add((ValueBase)hideValue);
        var action = new MissionAction(TriggerActions.Act_SetNNHidden, entry);

        var parsed = Assert.IsType<Act_SetNNHidden>(Assert.Single(ScriptedAction.Convert([action])));

        Assert.Equal("mlog_21855", parsed.Objective);
        Assert.Equal(expectedHide, parsed.Hide);
    }
}
