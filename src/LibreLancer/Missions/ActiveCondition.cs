using System;
using System.Collections.Generic;
using System.Linq;
using LancerNexus.Protocol;
using LibreLancer.Missions.Conditions;

namespace LibreLancer.Missions;

public class ActiveCondition
{
    public ActiveTrigger Trigger;
    public ScriptedCondition Condition;
    public ConditionStorage Storage = new ConditionEmpty();

    public ActiveCondition(ActiveTrigger trigger, ScriptedCondition condition)
    {
        Trigger = trigger;
        Condition = condition;
    }
}

public abstract class ConditionStorage
{
    public virtual NpcMissionConditionState CaptureTransferState() => new() { Kind = "none" };

    public virtual void RestoreTransferState(NpcMissionConditionState state)
    {
        if (state.Kind != "none")
            throw new InvalidOperationException($"Unsupported mission condition storage kind '{state.Kind}'.");
    }
}

// Stateless conditions still need an explicit snapshot kind for capture and restore.
public sealed class ConditionEmpty : ConditionStorage
{
}

public class ConditionBoolean : ConditionStorage
{
    public bool Value;
    public override NpcMissionConditionState CaptureTransferState() => new() { Kind = "boolean", BooleanValue = Value };
    public override void RestoreTransferState(NpcMissionConditionState state)
    {
        if (state.Kind != "boolean") throw new InvalidOperationException("Mission condition storage kind mismatch.");
        Value = state.BooleanValue;
    }
}

public class ConditionDouble : ConditionStorage
{
    public double Value;
    public override NpcMissionConditionState CaptureTransferState() => new() { Kind = "double", NumberValue = Value };
    public override void RestoreTransferState(NpcMissionConditionState state)
    {
        if (state.Kind != "double") throw new InvalidOperationException("Mission condition storage kind mismatch.");
        Value = state.NumberValue;
    }
}

public class ConditionHashSet : ConditionStorage
{
    public HashSet<string> Values = new(StringComparer.OrdinalIgnoreCase);
    public override NpcMissionConditionState CaptureTransferState() => new()
    {
        Kind = "strings",
        StringValues = Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
    };
    public override void RestoreTransferState(NpcMissionConditionState state)
    {
        if (state.Kind != "strings") throw new InvalidOperationException("Mission condition storage kind mismatch.");
        Values.Clear();
        Values.UnionWith(state.StringValues);
    }
}
