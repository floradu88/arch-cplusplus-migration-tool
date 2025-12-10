namespace SolutionDependencyMapper.Models;

/// <summary>
/// Represents the complete dependency graph of the solution.
/// </summary>
public class DependencyGraph
{
    public Dictionary<string, ProjectNode> Nodes { get; set; } = new();
    public List<DependencyEdge> Edges { get; set; } = new();
    public List<BuildLayer> BuildLayers { get; set; } = new();
    public List<List<string>> Cycles { get; set; } = new();
}

