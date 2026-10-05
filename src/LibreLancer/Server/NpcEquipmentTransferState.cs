using System;
using System.IO;
using LibreLancer.World;

namespace LibreLancer.Server;

internal sealed record NpcEquipmentTransferExtensionV1
{
    public NpcEquipmentTransferExtensionV1() { }
    public int SchemaVersion { get; init; } = 1;
    public NpcShieldTransferStateV1? Shield { get; init; }
    public double? WeaponCooldownSeconds { get; init; }
    public NpcWeaponAnglesTransferStateV1? WeaponAngles { get; init; }
    public float? ThrustCapacityFraction { get; init; }
}

internal sealed record NpcWeaponAnglesTransferStateV1(float Yaw, float Pitch)
{
    public NpcWeaponAnglesTransferStateV1() : this(0, 0) { }

    public void Validate()
    {
        if (!float.IsFinite(Yaw) || !float.IsFinite(Pitch))
            throw new InvalidDataException("NPC weapon angle transfer state is invalid.");
    }
}

internal sealed record NpcShieldTransferStateV1(float Health, double SuppressionRemainingSeconds,
    float SuppressedRestoreHealth)
{
    public NpcShieldTransferStateV1() : this(0, 0, 0) { }
}

internal sealed record NpcAiTransferExtensionV1
{
    public NpcAiTransferExtensionV1() { }
    public int SchemaVersion { get; init; } = 3;
    public NpcTransferObjectReferenceV1? DirectiveTarget { get; init; }
    public NpcTransferObjectReferenceV1? SelectedTarget { get; init; }
    public PIDControllerTransferState? SteeringRollController { get; init; }
    public NpcShipSteeringTransferStateV1? Steering { get; init; }
    public NpcShipPhysicsTransferStateV1? Physics { get; init; }
    public int? GotoKind { get; init; }
    public string CurrentState { get; init; } = "NULL";
    public string PreviousState { get; init; } = "NULL";
    public double TimeInState { get; init; }
    public double MissileTimer { get; init; }
    public float FireTimer { get; init; }
    public float BurstTimer { get; init; }
    public bool InBurst { get; init; }
    public int FireCycle { get; init; }
    public int WeaponGroupIndex { get; init; }
    public double DamageTimer { get; init; }
    public float DamageTaken { get; init; }
    public float EvadeX { get; init; }
    public float EvadeY { get; init; }
    public float EvadeZ { get; init; }
    public bool EvadeThrust { get; init; }
    public NpcTransferVectorV1 BuzzDirection { get; init; } = new(0, 0, 0);
    public NpcTransferObjectReferenceV1? StayInRangeTarget { get; init; }
    public NpcTransferVectorV1 StayInRangePoint { get; init; } = new(0, 0, 0);
    public float StayInRangeRadius { get; init; }
    public bool PopulationFleeing { get; init; }
    public ulong? TradeCargoUnitPrice { get; init; }
    public NpcAutoTurretTransferStateV1? AutoTurret { get; init; }
}

internal sealed record NpcAutoTurretTransferStateV1
{
    public float BurstTimer { get; init; }
    public float FireTimer { get; init; }
    public bool InBurst { get; init; }
    public ulong RandomState { get; init; }

    public void Validate()
    {
        if (!float.IsFinite(BurstTimer) || MathF.Abs(BurstTimer) > 3600 ||
            !float.IsFinite(FireTimer) || MathF.Abs(FireTimer) > 3600 || RandomState == 0)
            throw new InvalidDataException("NPC auto-turret transfer state is invalid.");
    }
}

internal sealed record NpcShipSteeringTransferStateV1
{
    public float Pitch { get; init; }
    public float Yaw { get; init; }
    public float Roll { get; init; }
    public float Throttle { get; init; }
    public bool Cruise { get; init; }
    public float CruiseSpeedOffset { get; init; }
    public bool Thrust { get; init; }
    public LibreLancer.World.Components.StrafeControls Strafe { get; init; }
    public bool EngineKill { get; init; }

    public void Validate()
    {
        const LibreLancer.World.Components.StrafeControls allControls = LibreLancer.World.Components.StrafeControls.Left |
            LibreLancer.World.Components.StrafeControls.Right | LibreLancer.World.Components.StrafeControls.Up |
            LibreLancer.World.Components.StrafeControls.Down;
        // Formation catch-up can request more than full engine throttle. Preserve that finite value.
        if (!float.IsFinite(Pitch) || !float.IsFinite(Yaw) || !float.IsFinite(Roll) ||
            !float.IsFinite(Throttle) || !float.IsFinite(CruiseSpeedOffset) ||
            Pitch is < -1 or > 1 || Yaw is < -1 or > 1 || Roll is < -1 or > 1 ||
            (Strafe & ~allControls) != 0 ||
            (Strafe & (LibreLancer.World.Components.StrafeControls.Left | LibreLancer.World.Components.StrafeControls.Right)) ==
                (LibreLancer.World.Components.StrafeControls.Left | LibreLancer.World.Components.StrafeControls.Right) ||
            (Strafe & (LibreLancer.World.Components.StrafeControls.Up | LibreLancer.World.Components.StrafeControls.Down)) ==
                (LibreLancer.World.Components.StrafeControls.Up | LibreLancer.World.Components.StrafeControls.Down))
            throw new InvalidDataException("NPC ship steering transfer state is invalid.");
    }
}

internal sealed record NpcShipPhysicsTransferStateV1
{
    public bool Active { get; init; }
    public LibreLancer.World.Components.EngineStates EngineState { get; init; }
    public bool ThrustRequested { get; init; }
    public bool ThrustDepleted { get; init; }
    public bool CruiseEnabled { get; init; }
    public bool PreviousCruiseEnabled { get; init; }
    public float CruiseSpeedOffset { get; init; }
    public bool EngineKillEnabled { get; init; }
    public float ChargePercent { get; init; }
    public float CruiseAccelPercent { get; init; }
    public float EnginePower { get; init; }
    public NpcTransferVectorV1 Steering { get; init; } = new(0, 0, 0);
    public LibreLancer.World.Components.StrafeControls Strafe { get; init; }
    public float EngineSpeed { get; init; }
    public bool EngineKill { get; init; }
    public LibreLancer.Net.Protocol.CruiseThrustState CruiseThrust { get; init; }

    public void Validate()
    {
        // Steering is rotated into world space; its components are not individual [-1, 1] controls.
        const LibreLancer.World.Components.StrafeControls allControls = LibreLancer.World.Components.StrafeControls.Left |
            LibreLancer.World.Components.StrafeControls.Right | LibreLancer.World.Components.StrafeControls.Up |
            LibreLancer.World.Components.StrafeControls.Down;
        if (!Enum.IsDefined(EngineState) || !Enum.IsDefined(CruiseThrust) ||
            !float.IsFinite(CruiseSpeedOffset) || !float.IsFinite(ChargePercent) ||
            ChargePercent is < 0 or > 1 || !float.IsFinite(CruiseAccelPercent) ||
            CruiseAccelPercent is < 0 or > 1 || !float.IsFinite(EnginePower) ||
            Steering is null || !float.IsFinite(Steering.X) || !float.IsFinite(Steering.Y) || !float.IsFinite(Steering.Z) ||
            !float.IsFinite(EngineSpeed) || (Strafe & ~allControls) != 0 ||
            (Strafe & (LibreLancer.World.Components.StrafeControls.Left | LibreLancer.World.Components.StrafeControls.Right)) ==
                (LibreLancer.World.Components.StrafeControls.Left | LibreLancer.World.Components.StrafeControls.Right) ||
            (Strafe & (LibreLancer.World.Components.StrafeControls.Up | LibreLancer.World.Components.StrafeControls.Down)) ==
                (LibreLancer.World.Components.StrafeControls.Up | LibreLancer.World.Components.StrafeControls.Down))
            throw new InvalidDataException("NPC ship physics transfer state is invalid.");
    }
}

internal sealed record NpcTransferObjectReferenceV1(string Kind, Guid? NpcId = null,
    long? CharacterId = null, string? Nickname = null)
{
    public NpcTransferObjectReferenceV1() : this("") { }
}

internal sealed record NpcTransferVectorV1(float X, float Y, float Z)
{
    public NpcTransferVectorV1() : this(0, 0, 0) { }
}
