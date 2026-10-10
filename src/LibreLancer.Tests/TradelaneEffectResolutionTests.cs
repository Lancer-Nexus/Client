using System.Runtime.CompilerServices;
using LibreLancer.Data;
using LibreLancer.Data.GameData;
using LibreLancer.Data.Schema.Equipment;
using Xunit;

namespace LibreLancer.Tests;

public class TradelaneEffectResolutionTests
{
    [Fact]
    public void ShipTravelEffectIsResolvedFromTradeLaneEquipmentIni()
    {
        var expected = (ResolvedFx)RuntimeHelpers.GetUninitializedObject(typeof(ResolvedFx));
        var definition = new Tradelane
        {
            TlShipTravel = "basic_tl_ship_travel"
        };

        var equipment = GameItemDb.CreateTradelaneEquipment(
            definition,
            name => name == "basic_tl_ship_travel" ? expected : null);

        Assert.Same(expected, equipment.ShipTravel);
    }
}
