using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Output;

/// <summary>
/// Generates MermaidJS diagram in Markdown format.
/// </summary>
public class MermaidGenerator
{
    /// <summary>
    /// Generates a MermaidJS diagram in Markdown format.
    /// </summary>
    /// <param name="graph">The dependency graph to visualize</param>
    /// <param name="outputPath">Path to the output Markdown file</param>
    public static void Generate(DependencyGraph graph, string outputPath)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("# Dependency Graph");
        sb.AppendLine();
        sb.AppendLine("This diagram shows the dependency relationships between projects in the solution.");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph TD");

        // Add nodes with styling based on output type and migration score
        foreach (var node in graph.Nodes.Values)
        {
            var nodeId = SanitizeNodeId(node.Name);
            var label = $"{node.Name}<br/>({node.OutputType})";
            
            // Add project type and ToolsVersion
            if (!string.IsNullOrWhiteSpace(node.ProjectType))
            {
                label += $"<br/>{node.ProjectType}";
            }
            if (!string.IsNullOrWhiteSpace(node.ToolsVersion))
            {
                label += $"<br/>ToolsVersion: {node.ToolsVersion}";
            }
            
            // Add migration score to label if available
            if (node.MigrationScore.HasValue)
            {
                label += $"<br/>Migration: {node.MigrationScore.Value}/100 ({node.MigrationDifficultyLevel ?? "Unknown"})";
            }
            
            sb.AppendLine($"    {nodeId}[\"{label}\"]");

            // Apply styling based on output type and migration difficulty
            var style = GetNodeStyle(node.OutputType, node.MigrationScore);
            if (!string.IsNullOrEmpty(style))
            {
                sb.AppendLine($"    style {nodeId} {style}");
            }
        }

        // Add edges
        foreach (var edge in graph.Edges)
        {
            if (graph.Nodes.ContainsKey(edge.FromProject) && 
                graph.Nodes.ContainsKey(edge.ToProject))
            {
                var fromId = SanitizeNodeId(graph.Nodes[edge.FromProject].Name);
                var toId = SanitizeNodeId(graph.Nodes[edge.ToProject].Name);
                sb.AppendLine($"    {fromId} --> {toId}");
            }
        }

        // Add external dependencies as separate nodes
        var externalDeps = new HashSet<string>();
        foreach (var node in graph.Nodes.Values)
        {
            foreach (var extDep in node.ExternalDependencies)
            {
                if (!string.IsNullOrWhiteSpace(extDep) && !extDep.Contains("\\") && !extDep.Contains("/"))
                {
                    externalDeps.Add(extDep);
                }
            }
        }

        foreach (var extDep in externalDeps.OrderBy(e => e))
        {
            var extId = SanitizeNodeId(extDep);
            sb.AppendLine($"    {extId}[\"{extDep}<br/>(External)\"]");
            sb.AppendLine($"    style {extId} fill:#ffd43b,stroke:#fab005,stroke-width:2px");
        }

        // Add edges from projects to external dependencies
        foreach (var node in graph.Nodes.Values)
        {
            var nodeId = SanitizeNodeId(node.Name);
            foreach (var extDep in node.ExternalDependencies)
            {
                if (!string.IsNullOrWhiteSpace(extDep) && !extDep.Contains("\\") && !extDep.Contains("/"))
                {
                    var extId = SanitizeNodeId(extDep);
                    sb.AppendLine($"    {nodeId} -.-> {extId}");
                }
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Legend");
        sb.AppendLine();
        sb.AppendLine("- **Red (rounded)**: Executables");
        sb.AppendLine("- **Green**: Dynamic Libraries (.dll, .so, .dylib)");
        sb.AppendLine("- **Blue**: Static Libraries (.lib, .a)");
        sb.AppendLine("- **Yellow**: External Dependencies");
        sb.AppendLine();
        sb.AppendLine("## Migration Scores");
        sb.AppendLine();
        sb.AppendLine("Migration scores indicate the difficulty of migrating each project to cross-platform (0-100, lower is easier).");
        sb.AppendLine();
        
        // Group projects by migration difficulty
        var projectsByDifficulty = graph.Nodes.Values
            .Where(p => p.MigrationScore.HasValue)
            .OrderBy(p => p.MigrationScore)
            .GroupBy(p => p.MigrationDifficultyLevel ?? "Unknown")
            .OrderBy(g => g.Key);

        foreach (var group in projectsByDifficulty)
        {
            sb.AppendLine($"### {group.Key} (Score: {group.Min(p => p.MigrationScore)}-{group.Max(p => p.MigrationScore)})");
            foreach (var project in group.OrderBy(p => p.MigrationScore))
            {
                sb.AppendLine($"- **{project.Name}**: {project.MigrationScore}/100 - {project.OutputType}");
            }
            sb.AppendLine();
        }

        if (!graph.Nodes.Values.Any(p => p.MigrationScore.HasValue))
        {
            sb.AppendLine("_No migration scores calculated._");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("## Reference Summary (Packages / Assemblies / Native / Includes)");
        sb.AppendLine();
        sb.AppendLine("This section summarizes the reference types found per project. Detailed lists are available in `dependency-tree.json`.");
        sb.AppendLine();

        foreach (var p in graph.Nodes.Values.OrderBy(p => p.Name))
        {
            sb.AppendLine($"- **{p.Name}**: " +
                          $"NuGet={p.NuGetPackageReferences.Count}, " +
                          $"FrameworkRefs={p.FrameworkReferences.Count}, " +
                          $"AssemblyRefs={p.AssemblyReferences.Count}, " +
                          $"COMRefs={p.ComReferences.Count}, " +
                          $"Analyzers={p.AnalyzerReferences.Count}, " +
                          $"NativeLibs={p.NativeLibraries.Count}, " +
                          $"DelayLoadDlls={p.NativeDelayLoadDlls.Count}, " +
                          $"IncludeDirs={p.IncludeDirectories.Count}, " +
                          $"Headers={p.HeaderFiles.Count}, " +
                          $"MissingRefs={p.ReferenceValidationIssues.Count}, " +
                          $"Configs={p.Configurations.Count}, " +
                          $"Platforms={p.Platforms.Count}");
        }

        sb.AppendLine();
        sb.AppendLine("## Project Types and ToolsVersion");
        sb.AppendLine();
        
        // Group by project type
        var projectsByType = graph.Nodes.Values
            .Where(p => !string.IsNullOrWhiteSpace(p.ProjectType))
            .GroupBy(p => p.ProjectType!)
            .OrderBy(g => g.Key);

        if (projectsByType.Any())
        {
            sb.AppendLine("### Project Types");
            foreach (var group in projectsByType)
            {
                sb.AppendLine($"- **{group.Key}**: {group.Count()} project(s)");
            }
            sb.AppendLine();
        }

        // Group by ToolsVersion
        var projectsByToolsVersion = graph.Nodes.Values
            .Where(p => !string.IsNullOrWhiteSpace(p.ToolsVersion))
            .GroupBy(p => p.ToolsVersion!)
            .OrderBy(g => g.Key);

        if (projectsByToolsVersion.Any())
        {
            sb.AppendLine("### ToolsVersion Distribution");
            foreach (var group in projectsByToolsVersion)
            {
                sb.AppendLine($"- **{group.Key}**: {group.Count()} project(s)");
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("## Build Layers");
        sb.AppendLine();
        
        foreach (var layer in graph.BuildLayers)
        {
            sb.AppendLine($"### Layer {layer.LayerNumber}");
            foreach (var projectPath in layer.ProjectPaths)
            {
                if (graph.Nodes.TryGetValue(projectPath, out var project))
                {
                    var projectInfo = $"{project.Name} ({project.OutputType})";
                    if (!string.IsNullOrWhiteSpace(project.ProjectType))
                    {
                        projectInfo += $" - {project.ProjectType}";
                    }
                    if (!string.IsNullOrWhiteSpace(project.ToolsVersion))
                    {
                        projectInfo += $" [ToolsVersion: {project.ToolsVersion}]";
                    }
                    sb.AppendLine($"- {projectInfo}");
                }
            }
            sb.AppendLine();
        }

        if (graph.Cycles.Count > 0)
        {
            sb.AppendLine("## ⚠️ Circular Dependencies Detected");
            sb.AppendLine();
            for (int i = 0; i < graph.Cycles.Count; i++)
            {
                sb.AppendLine($"### Cycle {i + 1}");
                var cycle = graph.Cycles[i];
                for (int j = 0; j < cycle.Count - 1; j++)
                {
                    if (graph.Nodes.TryGetValue(cycle[j], out var project))
                    {
                        sb.Append($"{project.Name} → ");
                    }
                }
                if (graph.Nodes.TryGetValue(cycle[cycle.Count - 1], out var lastProject))
                {
                    sb.AppendLine(lastProject.Name);
                }
                sb.AppendLine();
            }
        }

        File.WriteAllText(outputPath, sb.ToString());
    }

    private static string SanitizeNodeId(string name)
    {
        // Replace invalid characters for Mermaid node IDs
        return name
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace(".", "_")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "");
    }

    private static string GetNodeStyle(string outputType, int? migrationScore)
    {
        var baseStyle = outputType switch
        {
            "Exe" => "fill:#ff6b6b,stroke:#c92a2a,stroke-width:2px",
            "DynamicLibrary" => "fill:#51cf66,stroke:#2f9e44,stroke-width:2px",
            "StaticLibrary" => "fill:#339af0,stroke:#1c7ed6,stroke-width:2px",
            _ => string.Empty
        };

        // Add migration score indicator (border color based on difficulty)
        if (migrationScore.HasValue)
        {
            var borderColor = migrationScore.Value switch
            {
                < 20 => "#51cf66",  // Green for easy
                < 40 => "#ffd43b",  // Yellow for moderate
                < 60 => "#ff922b",  // Orange for hard
                < 80 => "#ff6b6b",  // Red for very hard
                _ => "#c92a2a"      // Dark red for extremely hard
            };
            
            // Modify stroke color to indicate migration difficulty
            if (!string.IsNullOrEmpty(baseStyle))
            {
                // Replace stroke color in existing style
                baseStyle = System.Text.RegularExpressions.Regex.Replace(
                    baseStyle, 
                    @"stroke:#[0-9a-fA-F]{6}", 
                    $"stroke:{borderColor}");
            }
            else
            {
                baseStyle = $"fill:#e1e1e1,stroke:{borderColor},stroke-width:3px";
            }
        }

        return baseStyle;
    }
}

