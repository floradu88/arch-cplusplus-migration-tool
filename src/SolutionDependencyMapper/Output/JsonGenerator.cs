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

            // Structured references
            NuGetPackageReferences = p.NuGetPackageReferences.Select(x => new
            {
                x.Id,
                x.Version,
                x.PrivateAssets,
                x.IncludeAssets,
                x.ExcludeAssets
            }),
            FrameworkReferences = p.FrameworkReferences,
            AssemblyReferences = p.AssemblyReferences,
            ComReferences = p.ComReferences,
            AnalyzerReferences = p.AnalyzerReferences,
            NativeLibraries = p.NativeLibraries,
            NativeDelayLoadDlls = p.NativeDelayLoadDlls,
            NativeLibraryDirectories = p.NativeLibraryDirectories,
            IncludeDirectories = p.IncludeDirectories,
            HeaderFiles = p.HeaderFiles,
            ReferenceValidationIssues = p.ReferenceValidationIssues.Select(i => new
            {
                i.Category,
                i.Reference,
                i.ResolvedPath,
                i.Details
            }),

            Configurations = p.Configurations,
            Platforms = p.Platforms,
            ConfigurationPlatforms = p.ConfigurationPlatforms,

            SolutionProjectGuid = p.SolutionProjectGuid,
            SolutionConfigurationMappings = p.SolutionConfigurationMappings.Select(m => new
            {
                Solution = new { m.Solution.Configuration, m.Solution.Platform, Key = m.Solution.Key },
                Project = new { m.Project.Configuration, m.Project.Platform, Key = m.Project.Key },
                m.Build,
                m.Deploy
            }),
            Properties = p.Properties,
            MigrationScore = p.MigrationScore,
            MigrationDifficultyLevel = p.MigrationDifficultyLevel
        }).ToList();

        var json = JsonSerializer.Serialize(projects, Options);
        File.WriteAllText(outputPath, json);
    }
}

