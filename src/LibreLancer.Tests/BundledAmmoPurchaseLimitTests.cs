using System.Collections.Generic;
using LibreLancer.Data.GameData;
using LibreLancer.Data.GameData.Items;
using LibreLancer.Data.Schema.Equipment;
using LibreLancer.Server;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public class BundledAmmoPurchaseLimitTests
{
    [Fact]
    public void SingleAutoMountedItemCanBePurchasedWithoutCargoSpaceForLauncher()
    {
        var launcher = new Equipment { Volume = 100 };
        var ship = new Ship { HoldSize = 0 };

        var limit = CargoUtilities.GetPurchaseLimit([], ship, launcher, maxByCredits: 5,
            canAutoMountSingle: true);

        Assert.Equal(1, limit);
    }

    [Fact]
    public void PurchaseLimitRespectsPerTypeAmmoStackLimit()
    {
        var launcher = new Equipment { Volume = 1 };
        var ammunition = new MunitionEquip { Def = new Munition(), Volume = 0.1f };
        var ship = new Ship { HoldSize = 100 };
        var cargo = new List<NetCargo>
        {
            new() { Equipment = ammunition, Count = 30 }
        };

        var limit = CargoUtilities.GetBundledAmmoPurchaseLimit(
            cargo, ship, launcher, ammunition, maxLaunchers: 10, canAutoMountSingle: false);

        Assert.Equal(2, limit);
    }

    [Fact]
    public void PurchaseLimitAccountsForLauncherAndBundledAmmoHoldSpace()
    {
        var launcher = new Equipment { Volume = 1 };
        var ammunition = new MunitionEquip { Def = new Munition(), Volume = 0.5f };
        var ship = new Ship { HoldSize = 15 };

        var limit = CargoUtilities.GetBundledAmmoPurchaseLimit(
            [], ship, launcher, ammunition, maxLaunchers: 10, canAutoMountSingle: false);

        Assert.Equal(2, limit);
    }

    [Fact]
    public void SingleAutoMountedLauncherDoesNotConsumeCargoVolume()
    {
        var launcher = new Equipment { Volume = 100 };
        var ammunition = new MunitionEquip { Def = new Munition(), Volume = 1 };
        var ship = new Ship { HoldSize = 10 };

        var limit = CargoUtilities.GetBundledAmmoPurchaseLimit(
            [], ship, launcher, ammunition, maxLaunchers: 10, canAutoMountSingle: true);

        Assert.Equal(1, limit);
    }
}
