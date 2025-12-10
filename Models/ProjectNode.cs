namespace SolutionDependencyMapper.Models;

/// <summary>
/// Represents a single project in the solution with its metadata and dependencies.
/// </summary>
public class ProjectNode
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string OutputType { get; set; } = string.Empty;
    public string OutputBinary { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string TargetExtension { get; set; } = string.Empty;
    public List<string> ProjectDependencies { get; set; } = new();
    public List<string> ExternalDependencies { get; set; } = new();
    public Dictionary<string, string> Properties { get; set; } = new();
    
    // Migration scoring (optional, calculated by MigrationScorer)
    public int? MigrationScore { get; set; }
    public string? MigrationDifficultyLevel { get; set; }
}

