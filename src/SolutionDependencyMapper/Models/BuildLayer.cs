namespace SolutionDependencyMapper.Models;

/// <summary>
/// Represents a build layer containing projects that can be built in parallel.
/// </summary>
public class BuildLayer
{
    public int LayerNumber { get; set; }
    public List<string> ProjectPaths { get; set; } = new();
}

