using System;
using System.Reflection;
using System.Text.Json;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcAiTransferStateTests
{
    [Fact]
    public void LiveFormationCatchupAndWorldSpaceSteeringRestoreWithoutChangingValues()
    {
        var assembly = typeof(GameServer).Assembly;
        var steeringType = assembly.GetType("LibreLancer.Server.NpcShipSteeringTransferStateV1", true)!;
        var physicsType = assembly.GetType("LibreLancer.Server.NpcShipPhysicsTransferStateV1", true)!;
        var vectorType = assembly.GetType("LibreLancer.Server.NpcTransferVectorV1", true)!;
        var steering = Activator.CreateInstance(steeringType)!;
        var physics = Activator.CreateInstance(physicsType)!;
        var vector = Activator.CreateInstance(vectorType)!;
        Set(steering, "Throttle", 1.1444763f);
        Set(vector, "X", 1.0632755f);
        Set(vector, "Y", 0.8913448f);
        Set(vector, "Z", -0.27376997f);
        Set(physics, "Steering", vector);

        steering = JsonSerializer.Deserialize(JsonSerializer.Serialize(steering, steeringType), steeringType)!;
        physics = JsonSerializer.Deserialize(JsonSerializer.Serialize(physics, physicsType), physicsType)!;
        steeringType.GetMethod("Validate")!.Invoke(steering, null);
        physicsType.GetMethod("Validate")!.Invoke(physics, null);
        Assert.Equal(1.1444763f, Get<float>(steering, "Throttle"));
        Assert.Equal(1.0632755f, Get<float>(Get<object>(physics, "Steering"), "X"));
        Assert.Equal(0.8913448f, Get<float>(Get<object>(physics, "Steering"), "Y"));
        Assert.Equal(-0.27376997f, Get<float>(Get<object>(physics, "Steering"), "Z"));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteLiveTransferControlsAreRejected(float invalid)
    {
        var assembly = typeof(GameServer).Assembly;
        var steeringType = assembly.GetType("LibreLancer.Server.NpcShipSteeringTransferStateV1", true)!;
        var physicsType = assembly.GetType("LibreLancer.Server.NpcShipPhysicsTransferStateV1", true)!;
        var vectorType = assembly.GetType("LibreLancer.Server.NpcTransferVectorV1", true)!;
        var steering = Activator.CreateInstance(steeringType)!;
        var physics = Activator.CreateInstance(physicsType)!;
        var vector = Activator.CreateInstance(vectorType)!;
        Set(steering, "Throttle", invalid);
        Set(vector, "X", invalid);
        Set(physics, "Steering", vector);
        var steeringFailure = Assert.Throws<TargetInvocationException>(() => steeringType.GetMethod("Validate")!.Invoke(steering, null));
        var physicsFailure = Assert.Throws<TargetInvocationException>(() => physicsType.GetMethod("Validate")!.Invoke(physics, null));
        Assert.IsType<System.IO.InvalidDataException>(steeringFailure.InnerException);
        Assert.IsType<System.IO.InvalidDataException>(physicsFailure.InnerException);
    }

    [Fact]
    public void VersionedAiStateAndStableTargetsRoundTripThroughJson()
    {
        var assembly = typeof(GameServer).Assembly;
        var stateType = assembly.GetType("LibreLancer.Server.NpcAiTransferExtensionV1", throwOnError: true)!;
        var targetType = assembly.GetType("LibreLancer.Server.NpcTransferObjectReferenceV1", throwOnError: true)!;
        var vectorType = assembly.GetType("LibreLancer.Server.NpcTransferVectorV1", throwOnError: true)!;
        var state = Activator.CreateInstance(stateType)!;
        var characterTarget = Activator.CreateInstance(targetType)!;
        Set(characterTarget, "Kind", "character");
        Set(characterTarget, "CharacterId", 42L);
        var stayTarget = Activator.CreateInstance(targetType)!;
        Set(stayTarget, "Kind", "nickname");
        Set(stayTarget, "Nickname", "mission_target");
        var direction = Activator.CreateInstance(vectorType)!;
        Set(direction, "X", 0.25f);
        Set(direction, "Y", -0.5f);
        Set(direction, "Z", 1.0f);
        var point = Activator.CreateInstance(vectorType)!;
        Set(point, "X", 120f);
        Set(point, "Y", -30f);
        Set(point, "Z", 400f);

        Set(state, "CurrentState", "Evade");
        Set(state, "PreviousState", "Buzz");
        Set(state, "TimeInState", 2.75d);
        Set(state, "MissileTimer", 0.4d);
        Set(state, "FireTimer", 0.2f);
        Set(state, "BurstTimer", 1.1f);
        Set(state, "InBurst", true);
        Set(state, "FireCycle", 3);
        Set(state, "WeaponGroupIndex", 2);
        Set(state, "DamageTimer", 0.8d);
        Set(state, "DamageTaken", 25f);
        Set(state, "EvadeX", -1f);
        Set(state, "EvadeY", 1f);
        Set(state, "EvadeThrust", true);
        Set(state, "DirectiveTarget", characterTarget);
        Set(state, "StayInRangeTarget", stayTarget);
        Set(state, "BuzzDirection", direction);
        Set(state, "StayInRangePoint", point);
        Set(state, "StayInRangeRadius", 75f);

        var json = JsonSerializer.Serialize(state, stateType);
        var restored = JsonSerializer.Deserialize(json, stateType);

        Assert.NotNull(restored);
        Assert.Equal(3, Get<int>(restored!, "SchemaVersion"));
        Assert.Equal("Evade", Get<string>(restored, "CurrentState"));
        Assert.Equal(2.75d, Get<double>(restored, "TimeInState"));
        Assert.Equal(0.2f, Get<float>(restored, "FireTimer"));
        Assert.Equal(1.1f, Get<float>(restored, "BurstTimer"));
        Assert.True(Get<bool>(restored, "InBurst"));
        Assert.Equal(3, Get<int>(restored, "FireCycle"));
        Assert.True(Get<bool>(restored, "EvadeThrust"));
        Assert.Equal(42L, Get<long>(Get<object>(restored, "DirectiveTarget"), "CharacterId"));
        Assert.Equal("mission_target", Get<string>(Get<object>(restored, "StayInRangeTarget"), "Nickname"));
        Assert.Equal(-0.5f, Get<float>(Get<object>(restored, "BuzzDirection"), "Y"));
        Assert.Equal(400f, Get<float>(Get<object>(restored, "StayInRangePoint"), "Z"));
        Assert.Equal(75f, Get<float>(restored, "StayInRangeRadius"));

        var equipmentType = assembly.GetType("LibreLancer.Server.NpcEquipmentTransferExtensionV1", throwOnError: true)!;
        var shieldType = assembly.GetType("LibreLancer.Server.NpcShieldTransferStateV1", throwOnError: true)!;
        var equipment = Activator.CreateInstance(equipmentType)!;
        var shield = Activator.CreateInstance(shieldType)!;
        Set(shield, "Health", 240f);
        Set(shield, "SuppressionRemainingSeconds", 3.5d);
        Set(shield, "SuppressedRestoreHealth", 360f);
        Set(equipment, "Shield", shield);
        var restoredEquipment = JsonSerializer.Deserialize(JsonSerializer.Serialize(equipment, equipmentType), equipmentType);

        Assert.NotNull(restoredEquipment);
        Assert.Equal(1, Get<int>(restoredEquipment!, "SchemaVersion"));
        var restoredShield = Get<object>(restoredEquipment, "Shield");
        Assert.Equal(240f, Get<float>(restoredShield, "Health"));
        Assert.Equal(3.5d, Get<double>(restoredShield, "SuppressionRemainingSeconds"));
        Assert.Equal(360f, Get<float>(restoredShield, "SuppressedRestoreHealth"));
    }

    private static void Set(object instance, string property, object value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(instance, value);

    private static T Get<T>(object instance, string property) =>
        (T)instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(instance)!;
}
