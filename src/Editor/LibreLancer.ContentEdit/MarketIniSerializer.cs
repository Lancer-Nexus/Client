using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibreLancer.Data;
using LibreLancer.Data.GameData;
using LibreLancer.Data.GameData.Items;
using LibreLancer.Data.GameData.World;
using LibreLancer.Data.Ini;
using LibreLancer.Data.Schema.Goods;

namespace LibreLancer.ContentEdit;

public static class MarketIniSerializer
{
    public record MarketFile(string Filename, Dictionary<string, List<BaseSoldGood>> BaseGoods);

    public static List<MarketFile> GetMarketFiles(
        GameItemDb items,
        IReadOnlyDictionary<string, HashSet<string>> dirtyFiles)
    {
        List<MarketFile> files = [];
        foreach (var dirtyFile in dirtyFiles)
        {
            if (string.IsNullOrWhiteSpace(dirtyFile.Key))
                continue;

            var marketFile = new MarketFile(dirtyFile.Key,
                new Dictionary<string, List<BaseSoldGood>>(StringComparer.OrdinalIgnoreCase));
            foreach (var baseName in dirtyFile.Value)
            {
                var @base = items.Bases.FirstOrDefault(x =>
                    x.Nickname.Equals(baseName, StringComparison.OrdinalIgnoreCase));
                if (@base == null)
                    continue;

                marketFile.BaseGoods[@base.Nickname] = @base.SoldGoods
                    .Where(x => x.Good.Ini.Category == GoodCategory.Commodity &&
                                string.Equals(x.SourceFile, dirtyFile.Key, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            files.Add(marketFile);
        }

        return files;
    }

    public static List<Section> MergeMarketFile(
        IEnumerable<Section> originalSections,
        MarketFile changes,
        Func<string, bool> isCommodity)
    {
        var sections = originalSections.ToList();
        var pending = changes.BaseGoods.ToDictionary(
            x => x.Key,
            x => x.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
        var targetSections = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in sections)
        {
            if (!section.Name.Equals("BaseGood", StringComparison.OrdinalIgnoreCase))
                continue;

            var baseEntry = section.FirstOrDefault(x => x.Name.Equals("base", StringComparison.OrdinalIgnoreCase));
            var baseName = baseEntry?.FirstOrDefault()?.ToString();
            if (string.IsNullOrWhiteSpace(baseName) || !pending.TryGetValue(baseName, out var baseChanges))
                continue;

            targetSections.TryAdd(baseName, section);
            foreach (var entry in section.Where(x => x.Name.Equals("marketgood", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (entry.Count == 0)
                    continue;

                var nickname = entry[0].ToString();
                var changeIndex = baseChanges.FindIndex(x =>
                    x.Good.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase));
                if (changeIndex >= 0)
                {
                    UpdateMarketGoodEntry(entry, baseChanges[changeIndex], changes.Filename);
                    baseChanges.RemoveAt(changeIndex);
                }
                else if (isCommodity(nickname))
                {
                    section.Remove(entry);
                }
            }
        }

        foreach (var baseChange in pending)
        {
            if (targetSections.TryGetValue(baseChange.Key, out var section))
            {
                foreach (var sold in baseChange.Value)
                    section.Add(CreateMarketGoodEntry(section, sold));
                continue;
            }

            section = new Section("BaseGood");
            section.Add(CreateEntry(section, "base", baseChange.Key));
            foreach (var sold in baseChange.Value)
                section.Add(CreateMarketGoodEntry(section, sold));
            sections.Add(section);
        }

        return sections;
    }

    private static void UpdateMarketGoodEntry(Entry entry, BaseSoldGood sold, string filename)
    {
        if (entry.Count < 7)
            throw new InvalidDataException(
                $"Cannot safely update malformed marketgood '{sold.Good.Nickname}' in {filename}.");

        entry[0] = sold.Good.Nickname;
        entry[1] = sold.Rank;
        entry[2] = sold.Rep;
        entry[3] = sold.Min;
        entry[4] = sold.Max;
        entry[5] = sold.Preserve ? 1 : 0;
        entry[6] = sold.Multiplier;
    }

    private static Entry CreateMarketGoodEntry(Section section, BaseSoldGood sold) =>
        CreateEntry(section, "marketgood",
            sold.Good.Nickname,
            sold.Rank,
            sold.Rep,
            sold.Min,
            sold.Max,
            sold.Preserve ? 1 : 0,
            sold.Multiplier);

    private static Entry CreateEntry(Section section, string name, params ValueBase[] values)
    {
        var entry = new Entry(section, name);
        foreach (var value in values)
            entry.Add(value);
        return entry;
    }
}
