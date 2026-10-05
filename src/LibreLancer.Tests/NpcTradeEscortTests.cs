using System;
using System.IO;
using LibreLancer.Data.Schema.Missions;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibreLancer.Data.GameData.World;
using LibreLancer.Data.GameData;
using LibreLancer.Server;
using LibreLancer.Data.IO;
using LLServer;
using Xunit;

namespace LibreLancer.Tests;

public class NpcTradeEscortTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1999, 0)]
    [InlineData(2000, 1)]
    [InlineData(9999, 1)]
    [InlineData(10000, 2)]
    [InlineData(49999, 2)]
    [InlineData(50000, 3)]
    public void Cargo_value_selects_required_escort_tier(long value, int expected)
    {
        Assert.Equal(expected, (int)SpacePopulationManager.GetCargoEscortTier((ulong)value));
    }

    [Fact]
    public void Scanned_illegal_cargo_increases_protection_value()
    {
        var commodity = new LibreLancer.Data.GameData.Items.CommodityEquipment();
        var cargo = new SpacePopulationManager.TradeCargoPlan(commodity, 10, 1600, Illegal: true, ScanSeverity: 2);

        Assert.Equal(2400UL, cargo.ProtectionValue);
        Assert.Equal(SpacePopulationManager.CargoEscortTier.Small,
            SpacePopulationManager.GetCargoEscortTier(cargo.ProtectionValue));
    }

    [Fact]
    public void Freelancer_scan_for_cargo_entries_classify_scanned_goods()
    {
        var scanned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["commodity_cardamine"] = 2,
            ["commodity_alien_artifacts"] = 1
        };

        Assert.Equal(2, SpacePopulationManager.GetCargoScanSeverity("COMMODITY_CARDAMINE", scanned));
        Assert.Equal(1, SpacePopulationManager.GetCargoScanSeverity("commodity_alien_artifacts", scanned));
        Assert.Equal(0, SpacePopulationManager.GetCargoScanSeverity("commodity_food", scanned));
    }

    [Theory]
    [InlineData(0.024, Legality.Lawful, true)]
    [InlineData(0.025, Legality.Lawful, false)]
    [InlineData(0.049, Legality.Unlawful, true)]
    [InlineData(0.05, Legality.Unlawful, false)]
    public void Illegal_cargo_smuggling_rolls_are_rare(double roll, Legality legality, bool expected)
    {
        Assert.Equal(expected, SpacePopulationManager.IsSmugglingRoll(roll, legality));
    }

    [Theory]
    [InlineData(80, 2, 40)]
    [InlineData(1, 2, 0)]
    [InlineData(1000, 0, 500)]
    public void Cargo_quantity_respects_ship_hold(float holdSize, float unitVolume, int expected)
    {
        Assert.Equal(expected, SpacePopulationManager.GetCargoQuantityLimit(holdSize, unitVolume));
    }

    [Fact]
    public void Fleeing_intent_survives_npc_ai_snapshot_serialization()
    {
        var stateType = typeof(GameServer).Assembly
            .GetType("LibreLancer.Server.NpcAiTransferExtensionV1", throwOnError: true)!;
        var state = Activator.CreateInstance(stateType)!;
        stateType.GetProperty("PopulationFleeing")!.SetValue(state, true);
        stateType.GetProperty("TradeCargoUnitPrice")!.SetValue(state, 360UL);

        var json = System.Text.Json.JsonSerializer.Serialize(state, stateType);
        var restored = System.Text.Json.JsonSerializer.Deserialize(json, stateType);

        Assert.NotNull(restored);
        Assert.True((bool)stateType.GetProperty("PopulationFleeing")!.GetValue(restored)!);
        Assert.Equal(360UL, (ulong)stateType.GetProperty("TradeCargoUnitPrice")!.GetValue(restored)!);
    }

    [Theory]
    [InlineData(360, 359, false)]
    [InlineData(360, 360, false)]
    [InlineData(360, 361, true)]
    public void Trade_destination_must_exceed_actual_source_market_price(ulong source, ulong destination, bool expected)
    {
        Assert.Equal(expected, SpacePopulationManager.IsProfitableTradeMarket(source, destination));
    }

    [Fact]
    public void Packaged_npc_data_overlay_overrides_freelancer_data_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "npc-overlay-" + Guid.NewGuid().ToString("N"));
        var freelancer = Path.Combine(root, "freelancer");
        var data = Path.Combine(root, "server", "data");
        var original = Path.Combine(freelancer, "EXE", "..", "data", "MISSIONS", "ENCOUNTERS");
        var overlay = Path.Combine(data, "MISSIONS", "ENCOUNTERS");
        Directory.CreateDirectory(Path.Combine(freelancer, "EXE"));
        Directory.CreateDirectory(original);
        Directory.CreateDirectory(overlay);
        File.WriteAllText(Path.Combine(original, "area_trade_transport.ini"), "original");
        File.WriteAllText(Path.Combine(overlay, "area_trade_transport.ini"), "overlay");

        try
        {
            var gameFiles = FileSystem.FromPath(freelancer);
            gameFiles.FileProviders.Add(new NpcDataOverlayFileProvider(data));
            Assert.Equal("overlay", gameFiles.ReadAllText(
                @"EXE\..\data\MISSIONS\ENCOUNTERS\area_trade_transport.ini"));
            Assert.Equal("overlay", gameFiles.ReadAllText(
                "data/MISSIONS/ENCOUNTERS/area_trade_transport.ini"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Patrol_paths_ignore_invalid_and_duplicate_waypoint_labels()
    {
        var zone = new Zone
        {
            PathLabel = ["local", "3", "bad", "-1", "1", "3"],
            Position = new Vector3(10, 20, 30)
        };

        Assert.Equal(new[] { 3, 1 }, SpacePopulationManager.PatrolPathSegments(zone)
            .Select(x => x.Label));
    }

    [Fact]
    public void Open_patrol_paths_terminate_and_closed_paths_loop()
    {
        var start = new Zone { Position = Vector3.Zero };
        var end = new Zone { Position = new Vector3(1000, 0, 0) };
        var open = new List<SpacePopulationManager.PatrolPathSegment>
        {
            new(start, 0), new(end, 1)
        };
        var closed = new List<SpacePopulationManager.PatrolPathSegment>
        {
            new(start, 0), new(end, 1), new(new Zone { Position = Vector3.Zero }, 2)
        };

        Assert.False(SpacePopulationManager.IsClosedPath(open));
        Assert.True(SpacePopulationManager.IsClosedPath(closed));
        Assert.True(SpacePopulationManager.IsPatrolPathExhausted(2, 1, closedLoop: false));
        Assert.False(SpacePopulationManager.IsPatrolPathExhausted(3, 1, closedLoop: false));
        Assert.False(SpacePopulationManager.IsPatrolPathExhausted(3, 2, closedLoop: true));
    }

    [Fact]
    public void Trade_route_hop_rejects_missing_or_looping_paths()
    {
        var current = new StarSystem { Nickname = "current", SourceFile = "current.ini" };
        var next = new StarSystem { Nickname = "next", SourceFile = "next.ini" };
        var destination = new StarSystem { Nickname = "destination", SourceFile = "destination.ini" };

        Assert.Same(next, SpacePopulationManager.GetNextTradeSystem([current, next, destination], current, destination));
        Assert.Null(SpacePopulationManager.GetNextTradeSystem([current, next, destination], current, destination, "next"));
        Assert.Null(SpacePopulationManager.GetNextTradeSystem([current, next, current, destination], current, destination));
        Assert.Null(SpacePopulationManager.GetNextTradeSystem([next, destination], current, destination));
    }

    [Theory]
    [InlineData("area_trade_transport.ini", "sc_transports", "cargo_transport")]
    [InlineData("area_trade_freighter.ini", "sc_freighters", "cargo_freighter")]
    public void Shipped_trade_encounter_ini_parses_cargo_carrier_roles(string filename, string shipClass, string role)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "NPCData", filename));
        var encounter = new EncounterIni();
        encounter.ParseIni(stream, filename);

        Assert.NotEmpty(encounter.Formations);
        Assert.All(encounter.Formations.Where(x => x.Behavior == EncounterBehavior.trade), formation =>
            Assert.Contains(formation.Ships, ship => ship.Archetype.Equals(shipClass, System.StringComparison.OrdinalIgnoreCase) &&
                                                     ship.MakeClass == role));
    }
}
