using System.Runtime.CompilerServices;
using LibreLancer.Thn;
using Xunit;

namespace LibreLancer.Tests;

public class ThnScriptDurationTests
{
    [Fact]
    public void FrameOvershootDoesNotRunEventsAfterScriptDuration()
    {
        var beforeEnd = new ProbeEvent(9);
        var afterEnd = new ProbeEvent(11);
        var script = new ThnScript
        {
            Duration = 10,
            Events = [beforeEnd, afterEnd]
        };
        var cutscene = (Cutscene)RuntimeHelpers.GetUninitializedObject(typeof(Cutscene));
        var instance = new ThnScriptInstance(cutscene, script);

        instance.Update(20);

        Assert.True(beforeEnd.Ran);
        Assert.False(afterEnd.Ran);
    }

    [Fact]
    public void FinishingScriptRunsOnlyInRangeEventsAndFinishesProcessors()
    {
        var inRange = new ProbeEvent(9);
        var afterEnd = new ProbeEvent(11);
        var script = new ThnScript { Duration = 10, Events = [inRange, afterEnd] };
        var instance = new ThnScriptInstance(
            (Cutscene)RuntimeHelpers.GetUninitializedObject(typeof(Cutscene)), script);
        var processor = new ProbeProcessor();
        instance.AddProcessor(processor);

        instance.FinishImmediate(raiseFinished: false);

        Assert.True(inRange.Ran);
        Assert.False(afterEnd.Ran);
        Assert.True(processor.Finished);
    }

    [Fact]
    public void LoopQueuesOneReplacementAtTheOverlapBoundary()
    {
        var script = new ThnScript { Duration = 10, Events = [] };
        var cutscene = (Cutscene)RuntimeHelpers.GetUninitializedObject(typeof(Cutscene));
        var instance = new ThnScriptInstance(cutscene, script)
        {
            Loop = true,
            SuppressFinishEvent = true
        };
        var replacements = 0;
        var finishedEvents = 0;
        instance.LoopReplacementRequested += _ => replacements++;
        cutscene.ScriptFinished += _ => finishedEvents++;

        instance.Update(7.99);
        Assert.Equal(0, replacements);
        instance.Update(0.01);
        instance.Update(0.25);
        instance.Update(2.01);

        Assert.Equal(1, replacements);
        Assert.True(instance.RemoveAfterFinish);
        Assert.Equal(0, finishedEvents);
    }

    private sealed class ProbeEvent : ThnEvent
    {
        public bool Ran { get; private set; }

        public ProbeEvent(float time) => Time = time;

        public override void Run(ThnScriptInstance instance) => Ran = true;
    }

    private sealed class ProbeProcessor : ThnEventProcessor
    {
        public bool Finished { get; private set; }

        public override bool Run(double delta) => true;

        public override void Finish() => Finished = true;
    }
}
