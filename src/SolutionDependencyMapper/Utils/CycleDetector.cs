using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Utils;

/// <summary>
/// Detects cycles in the dependency graph using DFS.
/// </summary>
public static class CycleDetector
{
    /// <summary>
    /// Detects all cycles in the dependency graph.
    /// </summary>
    /// <param name="graph">The dependency graph to analyze</param>
    /// <returns>List of cycles, where each cycle is a list of project paths</returns>
    public static List<List<string>> DetectCycles(DependencyGraph graph)
    {
        var cycles = new List<List<string>>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var nodePath in graph.Nodes.Keys)
        {
            if (!visited.Contains(nodePath))
            {
                DetectCyclesDFS(graph, nodePath, visited, recursionStack, path, cycles);
            }
        }

        return cycles;
    }

    private static void DetectCyclesDFS(
        DependencyGraph graph,
        string currentNode,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<List<string>> cycles)
    {
        visited.Add(currentNode);
        recursionStack.Add(currentNode);
        path.Add(currentNode);

        // Get all outgoing edges from current node
        var outgoingEdges = graph.Edges
            .Where(e => e.FromProject == currentNode && graph.Nodes.ContainsKey(e.ToProject))
            .Select(e => e.ToProject)
            .ToList();

        foreach (var neighbor in outgoingEdges)
        {
            if (!visited.Contains(neighbor))
            {
                DetectCyclesDFS(graph, neighbor, visited, recursionStack, path, cycles);
            }
            else if (recursionStack.Contains(neighbor))
            {
                // Cycle detected
                var cycleStart = path.IndexOf(neighbor);
                if (cycleStart >= 0)
                {
                    var cycle = path.Skip(cycleStart).Concat(new[] { neighbor }).ToList();
                    cycles.Add(cycle);
                }
            }
        }

        recursionStack.Remove(currentNode);
        path.RemoveAt(path.Count - 1);
    }
}

