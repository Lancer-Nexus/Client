using LibreLancer.Data.Schema.Equipment;
using Xunit;

namespace LibreLancer.Tests;

public sealed class HpTypesIniTests
{
    [Theory]
    [InlineData("hp_gun", HpCategory.Weapon)]
    [InlineData("hp_turret", HpCategory.Weapon)]
    [InlineData("hp_cargo_pod", HpCategory.External)]
    public void IncludesGenericVanillaHardpointTypes(string nickname, HpCategory category)
    {
        var hpTypes = new HpTypesIni();
        hpTypes.LoadDefault();

        Assert.True(hpTypes.Types.TryGetValue(nickname, out var hpType));
        Assert.Equal(category, hpType.Category);
        Assert.Equal(0, hpType.Class);
    }
}
