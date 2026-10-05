using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibreLancer.ContentEdit;
using LibreLancer.Data.GameData;
using LibreLancer.Data.GameData.Items;
using LibreLancer.Data.Ini;
using LibreLancer.Data.Schema.Goods;
using Xunit;

namespace LibreLancer.Tests;

public sealed class MarketIniSerializerTests
{
    private const string MarketPath = "DATA/EQUIPMENT/market_commodities.ini";
    private const string BaseName = "li01_01_base";

    [Fact]
    public void MergeRoundTripPreservesMarketFieldsAndUnrecognizedEntries()
    {
        var source = new Section("BaseGood");
        AddEntry(source, "base", BaseName);
        AddEntry(source, "marketgood", "commodity_gold", 1, -0.25f, 10, 50, 1, 1.25f, "extra-field");
        AddEntry(source, "marketgood", "commodity_removed", 2, 0f, 10, 10, 0, 1f);
        AddEntry(source, "marketgood", "unresolved_market_item", 0, -1f, 0, 0, 1, 1f);
        AddEntry(source, "vendor_extension", "keep-this-value");

        var unrelatedSection = new Section("VendorHints");
        AddEntry(unrelatedSection, "hint", "also-keep-this");

        var gold = SoldGood("commodity_gold", rank: 3, rep: -0.5f, price: 175,
            min: 10, max: 50, preserve: true, multiplier: 1.75f);
        var silver = SoldGood("commodity_silver", rank: 4, rep: 0f, price: 300,
            min: 0, max: 25, preserve: false, multiplier: 3f);
        var changes = new MarketIniSerializer.MarketFile(MarketPath,
            new Dictionary<string, List<BaseSoldGood>>(StringComparer.OrdinalIgnoreCase)
            {
                [BaseName] = [gold, silver]
            });

        var merged = MarketIniSerializer.MergeMarketFile(
            [source, unrelatedSection],
            changes,
            nickname => nickname is "commodity_gold" or "commodity_removed" or "commodity_silver");
        var reparsed = RoundTrip(merged);
        var baseSection = reparsed.Single(x => x.Name.Equals("BaseGood", StringComparison.OrdinalIgnoreCase));
        var goldEntry = MarketGood(baseSection, "commodity_gold");
        var silverEntry = MarketGood(baseSection, "commodity_silver");

        Assert.Equal(new[] { "commodity_gold", "3", "-0.5", "10", "50", "1", "1.75", "extra-field" },
            goldEntry.Select(x => x.ToString()).ToArray());
        Assert.Equal("extra-field", goldEntry[7].ToString());
        Assert.Equal(new[] { "commodity_silver", "4", "0", "0", "25", "0", "3" },
            silverEntry.Select(x => x.ToString()).ToArray());
        Assert.Contains(baseSection, x => x.Name == "vendor_extension" && x[0].ToString() == "keep-this-value");
        Assert.Contains(baseSection, x => x.Name == "marketgood" && x[0].ToString() == "unresolved_market_item");
        Assert.DoesNotContain(baseSection, x => x.Name == "marketgood" && x[0].ToString() == "commodity_removed");
        Assert.Contains(reparsed, x => x.Name == "VendorHints" && x[0][0].ToString() == "also-keep-this");
    }

    [Fact]
    public void MergeRejectsMalformedKnownRowsInsteadOfOverwritingThem()
    {
        var source = new Section("BaseGood");
        AddEntry(source, "base", BaseName);
        AddEntry(source, "marketgood", "commodity_gold", 1, 0f, 10);
        var changes = new MarketIniSerializer.MarketFile(MarketPath,
            new Dictionary<string, List<BaseSoldGood>>(StringComparer.OrdinalIgnoreCase)
            {
                [BaseName] = [SoldGood("commodity_gold", 1, 0, 100, 1, 1, false, 1f)]
            });

        Assert.Throws<InvalidDataException>(() => MarketIniSerializer.MergeMarketFile(
            [source], changes, _ => true));
    }

    private static Section[] RoundTrip(IEnumerable<Section> sections)
    {
        using var stream = new MemoryStream();
        IniWriter.WriteIni(stream, sections);
        stream.Position = 0;
        return IniFile.ParseFile("[roundtrip]", stream, true, false).ToArray();
    }

    private static Entry MarketGood(Section section, string nickname) =>
        section.Single(x => x.Name.Equals("marketgood", StringComparison.OrdinalIgnoreCase) &&
                            x.Count > 0 && x[0].ToString().Equals(nickname, StringComparison.OrdinalIgnoreCase));

    private static BaseSoldGood SoldGood(string nickname, int rank, float rep, ulong price,
        int min, int max, bool preserve, float multiplier)
    {
        var goodIni = new Good { Nickname = nickname, Price = 100, Category = GoodCategory.Commodity };
        var good = new ResolvedGood
        {
            Nickname = nickname,
            Ini = goodIni,
            Equipment = new Equipment { Nickname = nickname }
        };
        return new BaseSoldGood(rank, good, rep, price, max > 0, MarketPath,
            min, max, preserve, multiplier);
    }

    private static Entry AddEntry(Section section, string name, params ValueBase[] values)
    {
        var entry = new Entry(section, name);
        foreach (var value in values)
            entry.Add(value);
        section.Add(entry);
        return entry;
    }
}
