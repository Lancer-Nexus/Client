using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibreLancer.Data.GameData;
using LibreLancer.Data.GameData.Items;
using LibreLancer.Data.GameData.World;
using LibreLancer.Data.Schema.Missions;
using LibreLancer.Missions;
using LibreLancer.Missions.Directives;
using LibreLancer.Server.Components;
using LibreLancer.World;
using LibreLancer.World.Components;

namespace LibreLancer.Server;

public partial class SpacePopulationManager
{
    private const double IllegalCargoSpawnChance = 0.025;
    private const ulong SmallEscortCargoValue = 2_000;
    private const ulong HeavyEscortCargoValue = 10_000;
    private const ulong CapitalEscortCargoValue = 50_000;

    internal enum CargoEscortTier
    {
        None,
        Small,
        Heavy,
        Capital
    }

    internal static CargoEscortTier GetCargoEscortTier(ulong protectionValue) => protectionValue switch
    {
        >= CapitalEscortCargoValue => CargoEscortTier.Capital,
        >= HeavyEscortCargoValue => CargoEscortTier.Heavy,
        >= SmallEscortCargoValue => CargoEscortTier.Small,
        _ => CargoEscortTier.None
    };

    internal static int GetCargoQuantityLimit(float holdSize, float unitVolume)
    {
        if (!float.IsFinite(holdSize) || !float.IsFinite(unitVolume) || unitVolume <= 0)
            return 500;
        return Math.Clamp((int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0, holdSize) / unitVolume)), 0, 500);
    }

    internal static bool IsProfitableTradeMarket(ulong sourcePrice, ulong destinationPrice) =>
        destinationPrice > sourcePrice;

    internal static bool IsSmugglingRoll(double roll, Legality? legality) =>
        roll < (legality == Legality.Unlawful ? 0.05 : IllegalCargoSpawnChance);

    internal static int GetCargoScanSeverity(string commodity, IReadOnlyDictionary<string, int> scanned) =>
        Math.Max(0, scanned.GetValueOrDefault(commodity));

    private static bool IsHeavyEscort(ShipArch ship) => ship.NpcClass.Any(x =>
        x.Equals("elite_fighter", StringComparison.OrdinalIgnoreCase)) ||
        (ship.Level >= 8 && ship.NpcClass.Any(x =>
            x.Equals("class_fighter", StringComparison.OrdinalIgnoreCase)) &&
         (ship.Ship?.Nickname.Contains("fighter4", StringComparison.OrdinalIgnoreCase) == true ||
          ship.Ship?.Nickname.Contains("fighter5", StringComparison.OrdinalIgnoreCase) == true));

    internal static StarSystem? GetNextTradeSystem(
        IReadOnlyList<StarSystem> route,
        StarSystem current,
        StarSystem destination,
        string? previousSystem = null)
    {
        if (route.Count < 2 || !ReferenceEquals(route[0], current) ||
            !ReferenceEquals(route[^1], destination) || route.Distinct().Count() != route.Count ||
            previousSystem?.Equals(route[1].Nickname, StringComparison.OrdinalIgnoreCase) == true)
            return null;
        return route[1];
    }

    internal readonly record struct TradeCargoPlan(
        Equipment Commodity,
        int Quantity,
        ulong Value,
        bool Illegal,
        int ScanSeverity = 0,
        ulong SourceUnitPrice = 0)
    {
        public ulong ProtectionValue => Illegal
            ? Value + Value * (ulong)Math.Clamp(ScanSeverity, 1, 2) / 4
            : Value;
    }

    private bool TryLoadTradeCargo(EncounterInfo info, Faction faction, out TradeCargoPlan plan)
    {
        plan = default;
        if (info.FormationDefinition?.Behavior != EncounterBehavior.trade || info.Ships.Count == 0)
            return false;

        var db = world.Server.GameData.Items;
        var sourceMarkets = db.Bases
            .Where(x => x.System?.Equals(world.System.Nickname, StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(baseData => baseData.SoldGoods
                .Where(x => x.ForSale && x.Price >= 5 && x.Good.Equipment is CommodityEquipment)
                .Select(x => (Base: baseData, Market: x)))
            .ToArray();
        if (sourceMarkets.Length == 0)
            return false;

        var scanned = faction.Properties?.ScanForCargo
            .GroupBy(x => x.Cargo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Max(y => Math.Max(1, y.Param)), StringComparer.OrdinalIgnoreCase) ??
                      new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var maySmuggle = IsSmugglingRoll(random.NextDouble(), faction.Properties?.Legality);
        var goods = sourceMarkets
            .Where(x => maySmuggle || !scanned.ContainsKey(x.Market.Good.Equipment.Nickname))
            .ToArray();
        if (goods.Length == 0)
            return false;

        var profitable = goods
            .Select(x => new
            {
                x.Market.Good.Equipment,
                SourcePrice = x.Market.Price,
                MaxQuantity = GetCargoQuantityLimit(
                    info.Ships.FirstOrDefault(ship =>
                        ship.MakeClass?.Contains("freight", StringComparison.OrdinalIgnoreCase) == true ||
                        ship.MakeClass?.Contains("transport", StringComparison.OrdinalIgnoreCase) == true)
                        ?.Ship.Ship?.HoldSize ?? info.Ships[0].Ship.Ship?.HoldSize ?? 0,
                    x.Market.Good.Equipment.Volume),
                ScanSeverity = GetCargoScanSeverity(x.Market.Good.Equipment.Nickname, scanned),
                Illegal = GetCargoScanSeverity(x.Market.Good.Equipment.Nickname, scanned) > 0,
                DestinationPrice = db.Bases
                    .SelectMany(target => target.SoldGoods
                        .Where(g => g.ForSale && IsProfitableTradeMarket(x.Market.Price, g.Price) &&
                                    g.Good.Equipment.Nickname.Equals(x.Market.Good.Equipment.Nickname,
                                        StringComparison.OrdinalIgnoreCase))
                        .Select(market => (Base: target, Market: market)))
                    .Where(target => target.Base.System != null &&
                                     db.Systems.Get(target.Base.System) is { } system &&
                                     world.System.ShortestPathsAny.ContainsKey(system))
                    .Select(target => target.Market.Price)
                    .DefaultIfEmpty(0UL)
                    .Max()
            })
            .Where(x => x.MaxQuantity > 0 && x.DestinationPrice > x.SourcePrice)
            .ToArray();

        if (profitable.Length == 0)
            return false;

        var roll = random.NextDouble();
        var targetValue = roll < 0.15
            ? (ulong)random.Next(250, 1_999)
            : roll < 0.4
                ? (ulong)random.Next(2_000, 10_000)
                : roll < 0.8
                ? (ulong)random.Next(10_000, 50_000)
                : (ulong)random.Next(50_000, 100_001);
        var capable = profitable.Where(x => (ulong)x.MaxQuantity * x.SourcePrice >= targetValue).ToArray();
        var candidates = capable.Length > 0
            ? capable
            : profitable.OrderByDescending(x => (ulong)x.MaxQuantity * x.SourcePrice).Take(1).ToArray();
        var selected = candidates[random.Next(candidates.Length)];
        var requestedQuantity = (targetValue + selected.SourcePrice - 1) / selected.SourcePrice;
        var quantity = (int)Math.Min((ulong)selected.MaxQuantity, Math.Max(1UL, requestedQuantity));
        var value = (ulong)quantity * selected.SourcePrice;
        plan = new TradeCargoPlan(selected.Equipment, quantity, value, selected.Illegal, selected.ScanSeverity,
            selected.SourcePrice);

        AddCargoEscorts(info, faction, plan.ProtectionValue);
#if DEBUG
        var lightEscortCount = info.Ships.Count(ship => ship.Ship.NpcClass.Any(c =>
            c.Equals("class_fighter", StringComparison.OrdinalIgnoreCase)));
        var heavyEscortCount = info.Ships.Count(ship => IsHeavyEscort(ship.Ship));
        var capitalEscortCount = info.Ships.Count(ship => ship.MakeClass?.Equals(
            "capital_escort", StringComparison.OrdinalIgnoreCase) == true);
        FLLog.Info("SpacePop", $"Trade cargo {selected.Equipment.Nickname} x{quantity} at {selected.SourcePrice} = {value} " +
                               $"credits; illegal={selected.Illegal}; protection={plan.ProtectionValue}; tier={GetCargoEscortTier(plan.ProtectionValue)}; " +
                               $"escorts fighter={lightEscortCount} heavy={heavyEscortCount} capital={capitalEscortCount} faction={faction.Nickname}");
#endif
        return true;
    }

    private void AddCargoEscorts(EncounterInfo info, Faction faction, ulong protectionValue)
    {
        var tier = GetCargoEscortTier(protectionValue);
        if (tier == CargoEscortTier.None)
            return;

        EnsureEscortCount(info, faction, "class_fighter", 1);
        if (tier == CargoEscortTier.Small)
            return;

        EnsureEscortCount(info, faction, "class_fighter", 2);
        EnsureHeavyEscortCount(info, faction, 2);
        if (tier != CargoEscortTier.Capital)
            return;

        var capital = faction.NpcShips.FirstOrDefault(ship =>
            ship.NpcClass.Any(x => x.Equals("destroyer", StringComparison.OrdinalIgnoreCase) ||
                                  x.Equals("battleship", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(ship.Loadout) &&
            world.Server.GameData.Items.TryGetLoadout(ship.Loadout, out _));
        if (capital == null)
        {
            FLLog.Warning("SpacePop", $"Faction {faction.Nickname} has no valid destroyer or battleship loadout for high-value trader escort.");
            return;
        }
        if (info.Ships.All(x => !ReferenceEquals(x.Ship, capital)))
            info.Ships.Add(new(new ObjectName(""), null, capital, "capital_escort"));
    }

    private void BindTraderCombatReactions(PopGroup group)
    {
        if (!IsTradeGroup(group))
            return;
        foreach (var ship in group.Ships)
        {
            if (!ship.TryGetComponent<SHealthComponent>(out var health))
                continue;
            health.ProjectileHitHook += (_, attacker) => OnTraderGroupAttacked(group, attacker);
            health.KilledHook += attacker =>
            {
                if (attacker != null)
                    OnTraderGroupAttacked(group, attacker);
            };
        }
    }

    private void BindStandaloneTraderCombatReactions(GameObject[] ships)
    {
        if (!ships.Any(ship => ship.TryGetComponent<SNPCCargoComponent>(out var cargo) &&
                              cargo.Cargo.Any(x => x.Item is CommodityEquipment)))
            return;
        foreach (var ship in ships)
        {
            if (!ship.TryGetComponent<SHealthComponent>(out var health))
                continue;
            health.ProjectileHitHook += (_, attacker) => AttackStandaloneGroup(ships, attacker);
            health.KilledHook += attacker =>
            {
                if (attacker != null)
                    AttackStandaloneGroup(ships, attacker);
            };
        }
    }

    private void AttackStandaloneGroup(GameObject[] ships, GameObject attacker)
    {
        foreach (var ship in ships.Where(Alive))
        {
            ship.GetComponent<DirectiveRunnerComponent>()?.Cancel();
            if (ship.TryGetComponent<SNPCComponent>(out var npc))
                npc.Attack(attacker, world.GameWorld);
            if (ship.GetComponent<SelectedTargetComponent>() is { } selected)
                selected.Selected = attacker;
        }
    }

    private static bool IsTradeGroup(PopGroup group) => group.IsTransferred ||
        group.Encounter?.FormationDefinition?.Behavior == EncounterBehavior.trade;

    private void OnTraderGroupAttacked(PopGroup attacked, GameObject attacker)
    {
        if (!Alive(attacker))
            return;
        attacked.AlertTarget = attacker;
        attacked.InCombat = true;
        attacked.State.BattleCooldown = BattleCooldownSeconds;
        SuspendGroupDirectives(attacked);
        foreach (var ship in attacked.Ships.Where(Alive))
        {
            if (ship.TryGetComponent<SNPCComponent>(out var npc))
                npc.Attack(attacker, world.GameWorld);
            var selected = ship.GetComponent<SelectedTargetComponent>();
            if (selected != null)
                selected.Selected = attacker;
        }

        foreach (var state in zones)
        foreach (var other in state.Groups)
        {
            var attackedLeader = attacked.Ships.FirstOrDefault(Alive);
            var otherLeader = other.Ships.FirstOrDefault(Alive);
            if (ReferenceEquals(other, attacked) || !IsTradeGroup(other) ||
                attackedLeader == null || otherLeader == null ||
                System.Numerics.Vector3.DistanceSquared(attackedLeader.WorldTransform.Position,
                    otherLeader.WorldTransform.Position) > BattleDistance * BattleDistance ||
                !GroupsAreFriendly(attacked, other))
                continue;
            DirectGroupToFriendlyDock(other, attacked.Ships.FirstOrDefault(Alive)?.GetComponent<SRepComponent>()?.Faction);
        }
    }

    private static bool GroupsAreFriendly(PopGroup a, PopGroup b)
    {
        var aRep = a.Ships.FirstOrDefault(Alive)?.GetComponent<SRepComponent>();
        var bRep = b.Ships.FirstOrDefault(Alive)?.GetComponent<SRepComponent>();
        return aRep?.Faction != null && bRep?.Faction != null &&
               aRep.Faction.GetReputation(bRep.Faction) >= Faction.FriendlyThreshold;
    }

    private void DirectGroupToFriendlyDock(PopGroup group, Faction? threatenedFaction)
    {
        var leader = group.Ships.FirstOrDefault(Alive);
        if (leader == null)
            return;
        var route = BuildFriendlyEscapeRoute(group, leader.WorldTransform.Position, threatenedFaction);
        if (route.Length == 0)
            return;
        group.Fleeing = true;
        group.InCombat = false;
        SuspendGroupDirectives(group);
        foreach (var ship in group.Ships.Where(Alive))
        {
            if (ship.TryGetComponent<SNPCComponent>(out var npc))
                npc.PopulationFleeing = true;
            ship.GetComponent<DirectiveRunnerComponent>()?.SetDirectives(route, world.GameWorld);
        }
    }

    private MissionDirective[] BuildFriendlyEscapeRoute(PopGroup group, Vector3 position, Faction? faction) =>
        BuildFriendlyEscapeRoute(faction ?? group.Ships.FirstOrDefault(Alive)?.GetComponent<SRepComponent>()?.Faction,
            position);

    private MissionDirective[] BuildStandaloneFriendlyEscapeRoute(Vector3 position, Faction? faction) =>
        BuildFriendlyEscapeRoute(faction, position);

    private MissionDirective[] BuildFriendlyEscapeRoute(Faction? faction, Vector3 position)
    {
        if (FindFriendlyDockable(faction, position) is { } localDock)
            return BuildRetirementDirectives(localDock);

        var db = world.Server.GameData.Items;
        var destination = db.Bases
            .Where(baseData => baseData.System != null &&
                               db.Systems.Get(baseData.System) is { } system &&
                               world.System.ShortestPathsAny.ContainsKey(system) &&
                               (baseData.LocalFaction != null && faction != null &&
                                faction.GetReputation(baseData.LocalFaction) >= Faction.FriendlyThreshold ||
                                faction != null && baseData.BaseFactions.Any(x => x.Faction != null &&
                                    faction.GetReputation(x.Faction) >= Faction.FriendlyThreshold)))
            .Select(baseData => (Base: baseData, System: db.Systems.Get(baseData.System!)!))
            .OrderBy(x => world.System.ShortestPathsAny[x.System].Count)
            .FirstOrDefault();
        if (destination.Base == null ||
            !world.System.ShortestPathsAny.TryGetValue(destination.System, out var path) || path.Count < 2)
            return [];
        var gate = FindJumpGateTo(path[1].Nickname, position, null);
        return gate == null ? [] : BuildRetirementDirectives(gate);
    }

    private GameObject? FindFriendlyDockable(PopGroup group, Vector3 position, Faction? preferredFaction = null) =>
        FindFriendlyDockable(preferredFaction ?? group.Ships.FirstOrDefault(Alive)?.GetComponent<SRepComponent>()?.Faction,
            position);

    private GameObject? FindFriendlyDockable(Faction? leaderFaction, Vector3 position)
    {
        GameObject? nearestFriendly = null;
        var friendlyDistance = float.MaxValue;
        foreach (var obj in world.GameWorld.Objects)
        {
            if (!Alive(obj) || obj.SystemObject?.Base == null || string.IsNullOrWhiteSpace(obj.Nickname) ||
                obj.SystemObject.Archetype?.Nickname.Equals("docking_fixture", StringComparison.OrdinalIgnoreCase) == true ||
                !obj.TryGetComponent<SDockableComponent>(out var dockable) ||
                dockable.Action.Kind != DockKinds.Base || dockable.DockPoints.Length == 0)
                continue;
            var distance = System.Numerics.Vector3.DistanceSquared(position, obj.WorldTransform.Position);
            var baseData = obj.SystemObject.Base;
            var friendly = baseData.LocalFaction != null && leaderFaction != null &&
                           leaderFaction.GetReputation(baseData.LocalFaction) >= Faction.FriendlyThreshold ||
                           leaderFaction != null && baseData.BaseFactions.Any(x => x.Faction != null &&
                               leaderFaction.GetReputation(x.Faction) >= Faction.FriendlyThreshold);
            if (!friendly || distance >= friendlyDistance)
                continue;
            nearestFriendly = obj;
            friendlyDistance = distance;
        }
        return nearestFriendly;
    }

    private void EnsureEscortCount(EncounterInfo info, Faction faction, string npcClass, int minimum)
    {
        var count = info.Ships.Count(x => x.Ship.NpcClass.Any(c =>
            c.Equals(npcClass, StringComparison.OrdinalIgnoreCase)));
        var candidates = faction.NpcShips.Where(x =>
                x.NpcClass.Any(c => c.Equals(npcClass, StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(x.Loadout) &&
                world.Server.GameData.Items.TryGetLoadout(x.Loadout, out _))
            .ToArray();
        if (count < minimum && candidates.Length == 0)
        {
            FLLog.Warning("SpacePop", $"Faction {faction.Nickname} has no valid {npcClass} loadout for trader escort.");
            return;
        }
        while (count < minimum && candidates.Length > 0)
        {
            var arch = candidates[random.Next(candidates.Length)];
            var voice = faction.NpcVoices.Count > 0 ? faction.NpcVoices[random.Next(faction.NpcVoices.Count)] : null;
            info.Ships.Add(new(new ObjectName(""), voice, arch,
                npcClass.Equals("elite_fighter", StringComparison.OrdinalIgnoreCase)
                    ? "heavy_escort"
                    : "small_escort"));
            count++;
        }
    }

    private void EnsureHeavyEscortCount(EncounterInfo info, Faction faction, int minimum)
    {
        var count = info.Ships.Count(x => IsHeavyEscort(x.Ship));
        ShipArch[] ValidCandidates(IEnumerable<ShipArch> ships) => ships.Where(IsHeavyEscort).Where(x =>
            !string.IsNullOrWhiteSpace(x.Loadout) &&
            world.Server.GameData.Items.TryGetLoadout(x.Loadout, out _))
            .ToArray();
        var candidates = ValidCandidates(faction.NpcShips);
        if (count < minimum && candidates.Length == 0)
        {
            var legality = faction.Properties?.Legality;
            candidates = ValidCandidates(world.Server.GameData.Items.Factions
                .Where(other => other != faction && other.Properties?.Legality == legality)
                .SelectMany(other => other.NpcShips));
            if (candidates.Length > 0)
                FLLog.Warning("SpacePop", $"Faction {faction.Nickname} has no heavy fighter entry; using a compatible " +
                                         "same-legality fighter template for trader escort.");
        }
        if (count < minimum && candidates.Length == 0)
        {
            FLLog.Warning("SpacePop", $"Faction {faction.Nickname} has no valid heavy fighter loadout for trader escort.");
            return;
        }
        while (count < minimum && candidates.Length > 0)
        {
            var arch = candidates[random.Next(candidates.Length)];
            var voice = faction.NpcVoices.Count > 0 ? faction.NpcVoices[random.Next(faction.NpcVoices.Count)] : null;
            info.Ships.Add(new(new ObjectName(""), voice, arch, "heavy_escort"));
            count++;
        }
    }

    private MissionDirective[] BuildTradeCargoDirectives(IReadOnlyList<GameObject> ships, Vector3 currentPosition, string? excludeNickname)
    {
        var leader = ships.FirstOrDefault(Alive);
        if (leader == null || !leader.TryGetComponent<SNPCCargoComponent>(out var cargo) || cargo.Cargo.Count == 0)
            return [];

        var commodity = cargo.Cargo
            .Where(x => x.Item is CommodityEquipment && x.Count > 0)
            .OrderByDescending(x => (ulong)x.Count * (ulong)Math.Max(0, x.Item.Good?.Ini?.Price ?? 0))
            .Select(x => x.Item)
            .FirstOrDefault();
        if (commodity == null)
            return [];

        var db = world.Server.GameData.Items;
        var previousSystem = excludeNickname == null
            ? null
            : world.GameWorld.GetObject(excludeNickname)?.GetComponent<SDockableComponent>()?.Action.Target;
        var sourcePrice = ships.Select(ship => ship.GetComponent<SNPCComponent>()?.TradeCargoUnitPrice)
            .FirstOrDefault(price => price.HasValue) ?? (ulong)Math.Max(0, commodity.Good?.Ini?.Price ?? 0);
        var destinations = db.Bases
            .Where(baseData => baseData.System != null &&
                               db.Systems.Get(baseData.System) is { } system &&
                               world.System.ShortestPathsAny.ContainsKey(system))
            .SelectMany(baseData => baseData.SoldGoods
                .Where(good => good.ForSale && good.Good.Equipment.Nickname.Equals(
                    commodity.Nickname, StringComparison.OrdinalIgnoreCase) &&
                    IsProfitableTradeMarket(sourcePrice, good.Price))
                    .Select(good => (Base: baseData, Good: good)))
            .OrderByDescending(x => x.Good.Price);

        foreach (var destination in destinations)
        {
            var targetSystem = db.Systems.Get(destination.Base.System!);
            if (targetSystem != null && targetSystem.Nickname.Equals(world.System.Nickname,
                    StringComparison.OrdinalIgnoreCase))
            {
                var target = world.GameWorld.Objects.FirstOrDefault(obj =>
                    obj.SystemObject?.Base == destination.Base &&
                    obj.TryGetComponent<SDockableComponent>(out _) &&
                    Alive(obj) &&
                    (string.IsNullOrWhiteSpace(excludeNickname) ||
                     !excludeNickname.Equals(obj.Nickname, StringComparison.OrdinalIgnoreCase)));
                if (!string.IsNullOrWhiteSpace(target?.Nickname))
#if DEBUG
                {
                    FLLog.Info("SpacePop", $"Trade market route {commodity.Nickname}: source={sourcePrice}, " +
                                           $"destination={destination.Good.Price} at {destination.Base.Nickname} ({targetSystem.Nickname})");
#endif
                    return BuildRetirementDirectives(target);
#if DEBUG
                }
#endif
            }
            else if (targetSystem != null && world.System.ShortestPathsAny.TryGetValue(targetSystem, out var path) &&
                     GetNextTradeSystem(path, world.System, targetSystem, previousSystem) is { } nextSystem)
            {
                var gate = FindJumpGateTo(nextSystem.Nickname, currentPosition, excludeNickname);
                if (gate != null && !string.IsNullOrWhiteSpace(gate.Nickname))
#if DEBUG
                {
                    FLLog.Info("SpacePop", $"Trade market route {commodity.Nickname}: source={sourcePrice}, " +
                                           $"destination={destination.Good.Price} at {destination.Base.Nickname} " +
                                           $"({targetSystem.Nickname}) via {gate.Nickname}");
#endif
                    return BuildRetirementDirectives(gate);
#if DEBUG
                }
#endif
            }
        }
        return [];
    }

    private GameObject? FindJumpGateTo(string targetSystem, System.Numerics.Vector3 position, string? excludeNickname)
    {
        GameObject? nearest = null;
        var bestDistance = float.MaxValue;
        foreach (var obj in world.GameWorld.Objects)
        {
            if (!Alive(obj) || string.IsNullOrWhiteSpace(obj.Nickname) ||
                !obj.TryGetComponent<SDockableComponent>(out var dockable) ||
                !IsInterSystemPopulationJump(dockable.Action, world.System.Nickname) ||
                !dockable.Action.Target!.Equals(targetSystem, StringComparison.OrdinalIgnoreCase) ||
                excludeNickname?.Equals(obj.Nickname, StringComparison.OrdinalIgnoreCase) == true)
                continue;
            var distance = System.Numerics.Vector3.DistanceSquared(position, obj.WorldTransform.Position);
            if (distance >= bestDistance)
                continue;
            nearest = obj;
            bestDistance = distance;
        }
        return nearest;
    }
}
