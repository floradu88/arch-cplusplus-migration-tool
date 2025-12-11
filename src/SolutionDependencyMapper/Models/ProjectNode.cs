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
    
    // .NET Target Framework (for .csproj files)
    public string? TargetFramework { get; set; }
    
    // Project type (e.g., "C# Project", "C++ Project", "VB Project")
    public string? ProjectType { get; set; }
    
    // MSBuild ToolsVersion (e.g., "15.0", "16.0", "Current")
    public string? ToolsVersion { get; set; }
    
    // Migration scoring (optional, calculated by MigrationScorer)
    public int? MigrationScore { get; set; }
    public string? MigrationDifficultyLevel { get; set; }
}

