using System.Text.Json;
using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Output;

/// <summary>
/// Generates machine-readable JSON output from the dependency graph.
/// </summary>
public class JsonGenerator
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Generates a JSON file containing the dependency tree.
    /// </summary>
    /// <param name="graph">The dependency graph to serialize</param>
    /// <param name="outputPath">Path to the output JSON file</param>
    public static void Generate(DependencyGraph graph, string outputPath)
    {
        // Convert graph to a serializable format
        var projects = graph.Nodes.Values.Select(p => new
        {
            p.Name,
            Path = p.Path,
            ProjectType = p.ProjectType,
            ToolsVersion = p.ToolsVersion,
            OutputType = p.OutputType,
            OutputBinary = p.OutputBinary,
            TargetName = p.TargetName,
            TargetExtension = p.TargetExtension,
            TargetFramework = p.TargetFramework,
            ProjectDependencies = p.ProjectDependencies,
            ExternalDependencies = p.ExternalDependencies,
            Properties = p.Properties,
            MigrationScore = p.MigrationScore,
            MigrationDifficultyLevel = p.MigrationDifficultyLevel
        }).ToList();

        var json = JsonSerializer.Serialize(projects, Options);
        File.WriteAllText(outputPath, json);
    }
}

