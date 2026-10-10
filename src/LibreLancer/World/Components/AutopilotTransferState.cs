using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibreLancer.World.Components;

/// <summary>Process-independent subset of autopilot runtime state, mapped to Protocol at the transfer edge.</summary>
public sealed class AutopilotTransferState
{
    public ushort SchemaVersion { get; init; } = 2;
    public AutopilotBehaviors Behavior { get; init; }
    public PIDControllerTransferState? PitchController { get; set; }
    public PIDControllerTransferState? YawController { get; set; }
    public AutopilotAvoidanceTransferState? Avoidance { get; set; }
    public bool CanCruise { get; init; }
    public bool HasTriggeredCruise { get; init; }
    public float MaxThrottle { get; init; }
    public float GotoRadius { get; init; }
    public bool ShouldStopAtTarget { get; init; } = true;
    public Vector3 TargetPosition { get; init; }
    public float TargetRadius { get; init; }
    public Guid? TargetNpcId { get; init; }
    public long? TargetCharacterId { get; init; }
    public string? TargetNickname { get; init; }
    public int DockIndex { get; init; }
    public int DockLastTargetHardpoint { get; init; }
    public bool DockRingDocking { get; init; }
    public double DockRingTime { get; init; }
    public string? DockTradelaneHardpoint { get; init; }
    public bool DockTradelaneEntryPathActive { get; init; }
    public float DockTradelaneEntryPathProgress { get; init; }
    public Vector3 DockTradelaneEntryStart { get; init; }
    public Vector3 DockTradelaneEntryStartControl { get; init; }
    public Vector3 DockTradelaneEntryEndControl { get; init; }
    public Vector3 DockTradelaneEntryEnd { get; init; }
    public Vector3 DockTradelaneEntryAxis { get; init; }
    public int UndockIndex { get; init; }
    public double UndockTotalTime { get; init; }
    public double UndockDelay { get; init; }
    public int UndockTargetHardpoint { get; init; }
    public Vector3 FormationHeldSeparation { get; init; }
    public int FormationHeldSeparationNeighbor { get; init; }
    public float FormationSeparationHoldTimer { get; init; }

    internal LibreLancer.Server.NpcTransferObjectReferenceV1? GetTargetReference() =>
        TargetNpcId is { } npcId ? new("npc", NpcId: npcId) :
        TargetCharacterId is { } characterId ? new("character", CharacterId: characterId) :
        TargetNickname is { } nickname ? new("nickname", Nickname: nickname) : null;

    public void Validate()
    {
        var targetReferenceCount = (TargetNpcId.HasValue ? 1 : 0) +
                                   (TargetCharacterId.HasValue ? 1 : 0) +
                                   (TargetNickname is not null ? 1 : 0);
        if (SchemaVersion != 2 || Behavior is not (AutopilotBehaviors.None or AutopilotBehaviors.Goto or
                AutopilotBehaviors.Dock or AutopilotBehaviors.Formation or AutopilotBehaviors.Undock) ||
            PitchController is null || YawController is null ||
            !float.IsFinite(MaxThrottle) || MaxThrottle is < 0 or > 1 ||
            !float.IsFinite(GotoRadius) || GotoRadius < 0 ||
            !float.IsFinite(TargetPosition.X) || !float.IsFinite(TargetPosition.Y) ||
            !float.IsFinite(TargetPosition.Z) || !float.IsFinite(TargetRadius) || TargetRadius < 0 ||
            targetReferenceCount > 1 || Behavior == AutopilotBehaviors.None && targetReferenceCount != 0 ||
            Behavior == AutopilotBehaviors.Formation && targetReferenceCount != 0 ||
            (Behavior is AutopilotBehaviors.Dock or AutopilotBehaviors.Undock) && targetReferenceCount != 1 ||
            TargetNpcId == Guid.Empty || TargetCharacterId is <= 0 ||
            TargetNickname is { Length: 0 or > 96 } ||
            DockIndex < 0 || DockLastTargetHardpoint < 0 || !double.IsFinite(DockRingTime) || DockRingTime < 0 ||
            !float.IsFinite(DockTradelaneEntryPathProgress) || DockTradelaneEntryPathProgress is < 0 or > 1 ||
            !IsFinite(DockTradelaneEntryStart) || !IsFinite(DockTradelaneEntryStartControl) ||
            !IsFinite(DockTradelaneEntryEndControl) || !IsFinite(DockTradelaneEntryEnd) ||
            !IsFinite(DockTradelaneEntryAxis) ||
            DockTradelaneHardpoint is { Length: 0 or > 96 } ||
            UndockIndex < 0 || !double.IsFinite(UndockTotalTime) || UndockTotalTime < 0 ||
            !double.IsFinite(UndockDelay) || UndockDelay < 0 || UndockTargetHardpoint < 0 ||
            Behavior == AutopilotBehaviors.Undock && UndockTargetHardpoint < 1 ||
            !IsFinite(FormationHeldSeparation) || FormationHeldSeparationNeighbor < 0 ||
            !float.IsFinite(FormationSeparationHoldTimer) ||
            FormationSeparationHoldTimer is < 0 or > FormationControl.SeparationHoldTime ||
            Behavior == AutopilotBehaviors.None && Avoidance is not null ||
            Behavior != AutopilotBehaviors.None && Avoidance is null ||
            Behavior != AutopilotBehaviors.Dock &&
            (DockIndex != 0 || DockLastTargetHardpoint != 0 || DockRingDocking || DockRingTime != 0 ||
             DockTradelaneHardpoint is not null || DockTradelaneEntryPathActive || DockTradelaneEntryPathProgress != 0 ||
             DockTradelaneEntryStart != Vector3.Zero || DockTradelaneEntryStartControl != Vector3.Zero ||
             DockTradelaneEntryEndControl != Vector3.Zero || DockTradelaneEntryEnd != Vector3.Zero ||
             DockTradelaneEntryAxis != Vector3.Zero) ||
            Behavior != AutopilotBehaviors.Undock &&
            (UndockIndex != 0 || UndockTotalTime != 0 || UndockDelay != 0 || UndockTargetHardpoint != 0) ||
            Behavior != AutopilotBehaviors.Formation &&
            (FormationHeldSeparation != Vector3.Zero || FormationHeldSeparationNeighbor != 0 ||
             FormationSeparationHoldTimer != 0))
            throw new ArgumentException("Invalid or unsupported autopilot transfer state.");
        PitchController?.Validate();
        YawController?.Validate();
        Avoidance?.Validate();
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) &&
                                                   float.IsFinite(value.Z);
}

public sealed class AutopilotAvoidanceTransferState
{
    public StrafeControls Strafe { get; init; }
    [JsonConverter(typeof(AutopilotTransferVector2Converter))]
    public Vector2 Vector { get; init; }
    public float ClearTimer { get; init; }

    public void Validate()
    {
        const StrafeControls allControls = StrafeControls.Left | StrafeControls.Right |
                                           StrafeControls.Up | StrafeControls.Down;
        var hasDirection = Vector.LengthSquared() > float.Epsilon;
        if ((Strafe & ~allControls) != 0 || !float.IsFinite(Vector.X) || !float.IsFinite(Vector.Y) ||
            !float.IsFinite(ClearTimer) || ClearTimer is < 0 or > AutopilotObstacleAvoidance.AvoidanceClearDelay ||
            hasDirection != (Strafe != StrafeControls.None) || HasOpposingDirections(Strafe) ||
            Vector.LengthSquared() > 1.001f ||
            !hasDirection && ClearTimer != 0)
            throw new ArgumentException($"Invalid autopilot avoidance transfer state: strafe={(int)Strafe}, " +
                                        $"vector=({Vector.X:R},{Vector.Y:R}), lengthSquared={Vector.LengthSquared():R}, " +
                                        $"clearTimer={ClearTimer:R}.");
    }

    private static bool HasOpposingDirections(StrafeControls strafe) =>
        (strafe & (StrafeControls.Left | StrafeControls.Right)) ==
        (StrafeControls.Left | StrafeControls.Right) ||
        (strafe & (StrafeControls.Up | StrafeControls.Down)) ==
        (StrafeControls.Up | StrafeControls.Down);
}

public sealed class AutopilotTransferVector2Converter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Autopilot transfer vectors must be JSON objects.");
        var x = 0f;
        var y = 0f;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Autopilot transfer vector has an invalid property.");
            var property = reader.GetString();
            if (!reader.Read())
                throw new JsonException("Autopilot transfer vector is truncated.");
            if (property == "X")
                x = reader.GetSingle();
            else if (property == "Y")
                y = reader.GetSingle();
            else
                reader.Skip();
        }
        if (reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException("Autopilot transfer vector is unterminated.");
        return new Vector2(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteEndObject();
    }
}
