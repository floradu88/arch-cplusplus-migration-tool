using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Core;

/// <summary>
/// Parses individual project files (.vcxproj, .csproj) to extract metadata and dependencies.
/// </summary>
public class ProjectParser
{
    /// <summary>
    /// Ensures MSBuild is registered before parsing projects.
    /// Note: MSBuildLocator should be initialized in Program.Main before this is called.
    /// </summary>
    private static void EnsureMsBuildRegistered()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            throw new InvalidOperationException(
                "MSBuildLocator is not registered. This should be initialized in Program.Main before parsing projects."
            );
        }
    }

    /// <summary>
    /// Parses a project file and extracts all relevant information.
    /// </summary>
    /// <param name="projectPath">Path to the project file</param>
    /// <returns>ProjectNode with all extracted information, or null if parsing fails</returns>
    public static ProjectNode? ParseProject(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            Console.WriteLine($"Warning: Project file not found: {projectPath}");
            return null;
        }

        try
        {
            EnsureMsBuildRegistered();

            var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);

            var node = new ProjectNode
            {
                Path = projectPath,
                Name = project.GetPropertyValue("ProjectName") ?? Path.GetFileNameWithoutExtension(projectPath)
            };

            // Extract output type
            node.OutputType = ExtractOutputType(project);
            
            // Extract target information
            node.TargetName = project.GetPropertyValue("TargetName") ?? node.Name;
            node.TargetExtension = ExtractTargetExtension(project, node.OutputType);

            // Extract output binary path
            node.OutputBinary = ExtractOutputBinary(project, node.TargetName, node.TargetExtension);

            // Extract project references
            node.ProjectDependencies = ExtractProjectReferences(project, projectPath);

            // Extract external dependencies
            node.ExternalDependencies = ExtractExternalDependencies(project);

            // Extract additional properties
            node.Properties = ExtractProperties(project);

            projectCollection.UnloadProject(project);
            return node;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing project {projectPath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts the output type of the project (Exe, DynamicLibrary, StaticLibrary, etc.).
    /// </summary>
    private static string ExtractOutputType(Project project)
    {
        // For .vcxproj files, use ConfigurationType
        var configType = project.GetPropertyValue("ConfigurationType");
        if (!string.IsNullOrWhiteSpace(configType))
        {
            return configType switch
            {
                "Application" => "Exe",
                "DynamicLibrary" => "DynamicLibrary",
                "StaticLibrary" => "StaticLibrary",
                _ => configType
            };
        }

        // For .csproj files, use OutputType
        var outputType = project.GetPropertyValue("OutputType");
        if (!string.IsNullOrWhiteSpace(outputType))
        {
            return outputType switch
            {
                "Exe" => "Exe",
                "WinExe" => "Exe",
                "Library" => "DynamicLibrary",
                _ => outputType
            };
        }

        return "Unknown";
    }

    /// <summary>
    /// Extracts the target extension based on output type.
    /// </summary>
    private static string ExtractTargetExtension(Project project, string outputType)
    {
        var targetExt = project.GetPropertyValue("TargetExt");
        if (!string.IsNullOrWhiteSpace(targetExt))
        {
            return targetExt;
        }

        // Default extensions based on output type
        return outputType switch
        {
            "Exe" => ".exe",
            "DynamicLibrary" => project.FullPath.EndsWith(".vcxproj") ? ".dll" : ".dll",
            "StaticLibrary" => project.FullPath.EndsWith(".vcxproj") ? ".lib" : ".dll",
            _ => ".dll"
        };
    }

    /// <summary>
    /// Extracts the output binary path.
    /// </summary>
    private static string ExtractOutputBinary(Project project, string targetName, string targetExtension)
    {
        var outDir = project.GetPropertyValue("OutDir");
        if (string.IsNullOrWhiteSpace(outDir))
        {
            outDir = project.GetPropertyValue("OutputPath") ?? "bin\\";
        }

        // Normalize path separators
        outDir = outDir.Replace('\\', '/');
        if (!outDir.EndsWith("/"))
        {
            outDir += "/";
        }

        var outputPath = outDir + targetName + targetExtension;
        return outputPath;
    }

    /// <summary>
    /// Extracts project references and converts them to absolute paths.
    /// </summary>
    private static List<string> ExtractProjectReferences(Project project, string projectPath)
    {
        var references = new List<string>();
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;

        foreach (var item in project.GetItems("ProjectReference"))
        {
            var referencePath = item.EvaluatedInclude;
            var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, referencePath));
            references.Add(fullPath);
        }

        return references;
    }

    /// <summary>
    /// Extracts external dependencies (system libraries, NuGet packages, etc.).
    /// </summary>
    private static List<string> ExtractExternalDependencies(Project project)
    {
        var dependencies = new List<string>();

        // For .csproj: PackageReference and Reference items
        foreach (var item in project.GetItems("Reference"))
        {
            var include = item.EvaluatedInclude;
            if (!string.IsNullOrWhiteSpace(include))
            {
                dependencies.Add(include);
            }
        }

        // For .vcxproj: AdditionalDependencies (linker inputs)
        var additionalDeps = project.GetPropertyValue("AdditionalDependencies");
        if (!string.IsNullOrWhiteSpace(additionalDeps))
        {
            var deps = additionalDeps.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var dep in deps)
            {
                if (!string.IsNullOrWhiteSpace(dep) && !dep.StartsWith("%"))
                {
                    dependencies.Add(dep);
                }
            }
        }

        return dependencies.Distinct().ToList();
    }

    /// <summary>
    /// Extracts additional MSBuild properties that might be useful.
    /// </summary>
    private static Dictionary<string, string> ExtractProperties(Project project)
    {
        var properties = new Dictionary<string, string>();
        
        var importantProperties = new[]
        {
            "Configuration",
            "Platform",
            "PlatformTarget",
            "IntermediateOutputPath",
            "OutputPath",
            "RootNamespace",
            "AssemblyName"
        };

        foreach (var propName in importantProperties)
        {
            var value = project.GetPropertyValue(propName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                properties[propName] = value;
            }
        }

        return properties;
    }
}

