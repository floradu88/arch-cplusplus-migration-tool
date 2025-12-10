namespace SolutionDependencyMapper.Models;

/// <summary>
/// Represents a dependency relationship between two projects.
/// </summary>
public class DependencyEdge
{
    public string FromProject { get; set; } = string.Empty;
    public string ToProject { get; set; } = string.Empty;
    public string DependencyType { get; set; } = "ProjectReference";
}

