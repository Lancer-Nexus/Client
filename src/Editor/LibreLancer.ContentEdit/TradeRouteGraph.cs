using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreLancer.ContentEdit;

public static class TradeRouteGraph
{
    public static void AddDirectedConnection(Dictionary<string, List<string>> graph, string source, string target)
    {
        if (!graph.TryGetValue(source, out var targets))
            graph[source] = targets = [];
        if (!targets.Contains(target, StringComparer.OrdinalIgnoreCase))
            targets.Add(target);
        if (!graph.ContainsKey(target))
            graph[target] = [];
    }

    public static bool TryGetPath(
        IReadOnlyDictionary<string, List<string>> graph,
        string source,
        string target,
        out List<string> path)
    {
        path = [];
        if (!graph.ContainsKey(source) || !graph.ContainsKey(target))
            return false;

        Queue<string> queue = new();
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> previous = new(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue(source);
        visited.Add(source);
        previous[source] = null;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Equals(target, StringComparison.OrdinalIgnoreCase))
                break;

            foreach (var next in graph[current])
            {
                if (!visited.Add(next))
                    continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!previous.ContainsKey(target))
            return false;

        string? walker = target;
        while (walker != null)
        {
            path.Add(walker);
            walker = previous[walker];
        }
        path.Reverse();
        return true;
    }
}
