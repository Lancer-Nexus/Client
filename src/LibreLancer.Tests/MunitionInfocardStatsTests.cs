using System.Linq;
using LibreLancer.Client;
using LibreLancer.Data.GameData.Items;
using LibreLancer.Interface;
using LibreLancer.Infocards;
using LibreLancer.Data.Schema.Equipment;
using Xunit;

namespace LibreLancer.Tests;

public class MunitionInfocardStatsTests
{
    [Fact]
    public void AmmoDisplaysStatsInTwoColumnInfocard()
    {
        var ammunition = new MunitionEquip
        {
            Def = new Munition
            {
                HullDamage = 100,
                EnergyDamage = 250,
                Lifetime = 4,
                DetonationDist = 60,
                SeekerRange = 1200,
                SeekerFovDeg = 25,
                TimeToLock = 1.5f
            }
        };
        var item = new UIInventoryItem { Equipment = ammunition };

        var cards = new Trader(null!).GetEquipmentStats(item);

        Assert.NotNull(cards);
        Assert.Equal(3, cards.Length);
        var lines = cards.SelectMany(x => x!.Nodes.OfType<InfocardTextNode>())
            .Select(x => x.Contents)
            .ToArray();
        Assert.Contains("Hull Damage Per Shot:", lines);
        Assert.Contains("Seeker Range:", lines);
    }

    [Fact]
    public void MissileAmmoDisplaysStatsInTwoColumnInfocard()
    {
        var ammunition = new MissileEquip
        {
            Def = new Munition
            {
                HullDamage = 100,
                EnergyDamage = 250,
                Lifetime = 4,
                DetonationDist = 60,
                SeekerRange = 1200,
                SeekerFovDeg = 25,
                TimeToLock = 1.5f
            },
            Motor = null!,
            Explosion = null!
        };
        var item = new UIInventoryItem { Equipment = ammunition };

        var cards = new Trader(null!).GetEquipmentStats(item);

        Assert.NotNull(cards);
        Assert.Equal(3, cards.Length);
        var lines = cards.SelectMany(x => x!.Nodes.OfType<InfocardTextNode>())
            .Select(x => x.Contents)
            .ToArray();
        Assert.Contains("Hull Damage Per Shot:", lines);
        Assert.Contains("Seeker Range:", lines);
    }
}
