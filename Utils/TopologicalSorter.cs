using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Utils;

/// <summary>
/// Performs topological sorting on a dependency graph to determine build order.
/// </summary>
public static class TopologicalSorter
{
    /// <summary>
    /// Sorts projects into build layers using Kahn's algorithm.
    /// Projects in the same layer can be built in parallel.
    /// </summary>
    /// <param name="graph">The dependency graph to sort</param>
    /// <returns>List of build layers in topological order</returns>
    public static List<BuildLayer> SortIntoLayers(DependencyGraph graph)
    {
        var layers = new List<BuildLayer>();
        var inDegree = new Dictionary<string, int>();
        var adjacencyList = new Dictionary<string, List<string>>();

        // Initialize in-degree count and adjacency list
        foreach (var node in graph.Nodes.Values)
        {
            inDegree[node.Path] = 0;
            adjacencyList[node.Path] = new List<string>();
        }

        // Build adjacency list and count in-degrees
        foreach (var edge in graph.Edges)
        {
            if (graph.Nodes.ContainsKey(edge.FromProject) && 
                graph.Nodes.ContainsKey(edge.ToProject))
            {
                adjacencyList[edge.ToProject].Add(edge.FromProject);
                inDegree[edge.FromProject]++;
            }
        }

        // Kahn's algorithm
        var currentLayer = new Queue<string>();
        var processed = new HashSet<string>();

        // Start with nodes that have no dependencies (in-degree = 0)
        foreach (var kvp in inDegree)
        {
            if (kvp.Value == 0)
            {
                currentLayer.Enqueue(kvp.Key);
                processed.Add(kvp.Key);
            }
        }

        int layerNumber = 0;
        while (currentLayer.Count > 0)
        {
            var layer = new BuildLayer
            {
                LayerNumber = layerNumber++,
                ProjectPaths = new List<string>()
            };

            var nextLayer = new Queue<string>();
            var layerSize = currentLayer.Count;

            // Process all nodes in current layer
            for (int i = 0; i < layerSize; i++)
            {
                var projectPath = currentLayer.Dequeue();
                layer.ProjectPaths.Add(projectPath);

                // Decrease in-degree of dependent projects
                foreach (var dependent in adjacencyList[projectPath])
                {
                    if (processed.Contains(dependent))
                        continue;

                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                    {
                        nextLayer.Enqueue(dependent);
                        processed.Add(dependent);
                    }
                }
            }

            layers.Add(layer);
            currentLayer = nextLayer;
        }

        // Check for cycles (unprocessed nodes)
        var unprocessed = graph.Nodes.Keys.Where(k => !processed.Contains(k)).ToList();
        if (unprocessed.Count > 0)
        {
            Console.WriteLine($"Warning: {unprocessed.Count} projects could not be sorted (possible cycles):");
            foreach (var path in unprocessed)
            {
                Console.WriteLine($"  - {path}");
            }
        }

        return layers;
    }
}

