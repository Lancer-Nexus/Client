using System;
using System.Collections.Generic;
using LibreLancer.ContentEdit;
using Xunit;

namespace LibreLancer.Tests;

public sealed class TradeRouteGraphTests
{
    [Fact]
    public void FindsDirectedPathsWithoutInventingReturnJumps()
    {
        Dictionary<string, List<string>> graph = new(StringComparer.OrdinalIgnoreCase);
        TradeRouteGraph.AddDirectedConnection(graph, "li01", "li02");
        TradeRouteGraph.AddDirectedConnection(graph, "li02", "li03");

        Assert.True(TradeRouteGraph.TryGetPath(graph, "LI01", "li03", out var path));
        Assert.Equal(new[] { "LI01", "li02", "li03" }, path);
        Assert.False(TradeRouteGraph.TryGetPath(graph, "li03", "li01", out _));
    }

    [Fact]
    public void StopsOnCyclesAndReturnsTheShortestReachablePath()
    {
        Dictionary<string, List<string>> graph = new(StringComparer.OrdinalIgnoreCase);
        TradeRouteGraph.AddDirectedConnection(graph, "a", "b");
        TradeRouteGraph.AddDirectedConnection(graph, "b", "a");
        TradeRouteGraph.AddDirectedConnection(graph, "a", "c");
        TradeRouteGraph.AddDirectedConnection(graph, "c", "d");
        TradeRouteGraph.AddDirectedConnection(graph, "b", "d");

        Assert.True(TradeRouteGraph.TryGetPath(graph, "a", "d", out var path));
        Assert.Equal(new[] { "a", "b", "d" }, path);
    }
}
