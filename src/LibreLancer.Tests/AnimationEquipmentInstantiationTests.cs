using LibreLancer.Data.GameData.Items;
using LibreLancer.World;
using LibreLancer.World.Components;
using Xunit;

namespace LibreLancer.Tests;

public class AnimationEquipmentInstantiationTests
{
    [Fact]
    public void AnimationEquipmentDoesNotReachVisualEquipmentFactory()
    {
        var ship = new GameObject();
        var animation = new AnimationEquipment
        {
            Nickname = "fixture_animation",
            Animation = "Sc_fire"
        };

        EquipmentObjectManager.InstantiateEquipment(
            ship,
            null!,
            null,
            EquipmentType.Server,
            "HpWeapon01",
            animation);

        Assert.Empty(ship.Children);
        Assert.False(ship.TryGetComponent<EquipmentComponent>(out _));
    }
}
