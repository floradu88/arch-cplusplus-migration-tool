using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Utils;

namespace SolutionDependencyMapper.Core;

/// <summary>
/// Parses individual project files (.vcxproj, .csproj) to extract metadata and dependencies.
/// </summary>
public class ProjectParser
{
    /// <summary>
    /// Ensures MSBuild is registered before parsing projects.
    /// Note: MSBuildLocator should be initialized in Program.Main before this is called.
    /// If --assume-vs-env flag is used, this check is relaxed to allow direct MSBuild API usage.
    /// </summary>
    private static void EnsureMsBuildRegistered(bool assumeVsEnv = false)
    {
        if (!assumeVsEnv && !MSBuildLocator.IsRegistered)
        {
            throw new InvalidOperationException(
                "MSBuildLocator is not registered. This should be initialized in Program.Main before parsing projects. " +
                "Or use --assume-vs-env flag if running from VS Developer Command Prompt."
            );
        }
    }

    /// <summary>
    /// Parses a project file and extracts all relevant information.
    /// Includes retry logic with automatic package installation for missing Microsoft.Build dependencies.
    /// </summary>
    /// <param name="projectPath">Path to the project file</param>
    /// <param name="assumeVsEnv">If true, skip MSBuildLocator check (assumes VS environment is configured)</param>
    /// <param name="maxRetries">Maximum number of retry attempts after package installation (default: 1)</param>
    /// <param name="autoInstallPackages">If true, automatically install missing Microsoft.Build packages (default: true)</param>
    /// <returns>ProjectNode with all extracted information, or null if parsing fails</returns>
    public static ProjectNode? ParseProject(string projectPath, bool assumeVsEnv = false, int maxRetries = 1, bool autoInstallPackages = true)
    {
        if (!File.Exists(projectPath))
        {
            Console.WriteLine($"Warning: Project file not found: {projectPath}");
            return null;
        }

        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= maxRetries)
        {
            try
            {
                EnsureMsBuildRegistered(assumeVsEnv);

                var projectCollection = new ProjectCollection();
                var project = projectCollection.LoadProject(projectPath);

                var node = new ProjectNode
                {
                    Path = projectPath,
                    Name = project.GetPropertyValue("ProjectName") ?? Path.GetFileNameWithoutExtension(projectPath)
                };

                // Extract project type from file extension
                node.ProjectType = ExtractProjectType(projectPath);
                
                // Extract ToolsVersion
                node.ToolsVersion = ExtractToolsVersion(project, projectPath);

                // Extract output type
                node.OutputType = ExtractOutputType(project);
                
                // Extract target information
                node.TargetName = project.GetPropertyValue("TargetName") ?? node.Name;
                node.TargetExtension = ExtractTargetExtension(project, node.OutputType);

                // Extract output binary path
                node.OutputBinary = ExtractOutputBinary(project, node.TargetName, node.TargetExtension);

                // Extract project references
                node.ProjectDependencies = ExtractProjectReferences(project, projectPath);

                // Extract structured references
                ExtractStructuredReferences(project, projectPath, node);

                // Extract backward-compatible flat dependencies (used by existing outputs)
                node.ExternalDependencies = BuildFlatExternalDependencies(node);

                // Validate that key file/path-based references exist (non-building check)
                ReferenceValidator.Validate(project, projectPath, node);

                // Extract additional properties
                node.Properties = ExtractProperties(project);

                // Extract TargetFramework for .NET projects
                node.TargetFramework = ExtractTargetFramework(project);

                projectCollection.UnloadProject(project);
                return node;
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Check if this is a missing package error and we haven't exceeded retries
                if (retryCount < maxRetries && PackageInstaller.IsMissingPackageError(ex))
                {
                    if (autoInstallPackages)
                    {
                        Console.WriteLine($"  ⚠️  Detected missing package error for {Path.GetFileName(projectPath)}");
                        Console.WriteLine($"     Attempting to install missing Microsoft.Build packages...");

                        // Try to fix the project by installing packages and restoring
                        if (PackageInstaller.FixProjectPackagesForError(projectPath, ex))
                        {
                            retryCount++;
                            Console.WriteLine($"     Retrying parse (attempt {retryCount + 1}/{maxRetries + 1})...");
                            
                            // Wait a bit for file system to catch up
                            System.Threading.Thread.Sleep(500);
                            continue;
                        }
                        else
                        {
                            Console.WriteLine($"     Could not automatically fix packages. Error: {ex.Message}");
                            break;
                        }
                    }
                    else
                    {
                        // Auto-installation is disabled, just report the error
                        Console.WriteLine($"  ⚠️  Detected missing package error for {Path.GetFileName(projectPath)}");
                        Console.WriteLine($"     Automatic package installation is disabled. Use --auto-install-packages to enable.");
                        break;
                    }
                }
                else
                {
                    // Not a package error or retries exhausted
                    Console.WriteLine($"Error parsing project {projectPath}: {ex.Message}");
                    if (retryCount > 0)
                    {
                        Console.WriteLine($"  (Failed after {retryCount + 1} attempt(s))");
                    }
                    return null;
                }
            }
        }

        // If we get here, all retries failed
        if (lastException != null)
        {
            Console.WriteLine($"Error parsing project {projectPath} after {retryCount + 1} attempt(s): {lastException.Message}");
        }
        return null;
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

    private static void ExtractStructuredReferences(Project project, string projectPath, ProjectNode node)
    {
        // Managed references
        node.NuGetPackageReferences = ExtractNuGetPackageReferences(project);
        node.FrameworkReferences = ExtractItemIncludes(project, "FrameworkReference");
        node.AssemblyReferences = ExtractItemIncludes(project, "Reference");
        node.ComReferences = ExtractItemIncludes(project, "COMReference");
        node.AnalyzerReferences = ExtractItemIncludes(project, "Analyzer");

        // Native references
        node.NativeLibraries = ExtractListProperty(project, "AdditionalDependencies");
        node.NativeDelayLoadDlls = ExtractListProperty(project, "DelayLoadDLLs");
        node.NativeLibraryDirectories = ExtractListProperty(project, "AdditionalLibraryDirectories");
        node.IncludeDirectories = ExtractListProperty(project, "AdditionalIncludeDirectories");
        node.HeaderFiles = ExtractProjectItemPaths(project, projectPath, "ClInclude");
    }

    private static List<string> BuildFlatExternalDependencies(ProjectNode node)
    {
        // Keep the existing output behavior (external nodes, etc.) stable, but enrich it with the most relevant lists.
        // NOTE: We intentionally do NOT include NuGet packages here to avoid huge diagrams by default.
        var list = new List<string>();
        list.AddRange(node.AssemblyReferences);
        list.AddRange(node.FrameworkReferences);
        list.AddRange(node.ComReferences);
        list.AddRange(node.NativeLibraries);
        list.AddRange(node.NativeDelayLoadDlls);
        return list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractItemIncludes(Project project, string itemType)
    {
        var items = new List<string>();
        foreach (var item in project.GetItems(itemType))
        {
            var include = item.EvaluatedInclude;
            if (!string.IsNullOrWhiteSpace(include))
            {
                items.Add(include.Trim());
            }
        }
        return items.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<NuGetPackageReference> ExtractNuGetPackageReferences(Project project)
    {
        var packages = new List<NuGetPackageReference>();

        foreach (var item in project.GetItems("PackageReference"))
        {
            var id = item.EvaluatedInclude?.Trim();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var version = item.GetMetadataValue("Version");
            if (string.IsNullOrWhiteSpace(version))
            {
                // Some projects keep version in central package management or props; keep null in that case.
                version = null;
            }

            var pkg = new NuGetPackageReference
            {
                Id = id,
                Version = version,
                PrivateAssets = NormalizeMetadata(item.GetMetadataValue("PrivateAssets")),
                IncludeAssets = NormalizeMetadata(item.GetMetadataValue("IncludeAssets")),
                ExcludeAssets = NormalizeMetadata(item.GetMetadataValue("ExcludeAssets"))
            };

            packages.Add(pkg);
        }

        // Distinct by package ID (keep first version/metadata encountered)
        return packages
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeMetadata(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> ExtractListProperty(Project project, string propertyName)
    {
        var raw = project.GetPropertyValue(propertyName);
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Drop MSBuild "append" macros like %(AdditionalIncludeDirectories)
        return parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Where(p => !p.StartsWith("%(", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExtractProjectItemPaths(Project project, string projectPath, string itemType)
    {
        var results = new List<string>();
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;

        foreach (var item in project.GetItems(itemType))
        {
            var include = item.EvaluatedInclude?.Trim();
            if (string.IsNullOrWhiteSpace(include))
                continue;

            // Most vcxproj items are relative to the project directory.
            var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, include));
            results.Add(fullPath);
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Extracts the target framework for .NET projects (.csproj).
    /// Supports .NET Framework, .NET Core, .NET 5+, .NET 8, .NET 9, and .NET 10 (when available).
    /// </summary>
    private static string? ExtractTargetFramework(Project project)
    {
        // Try TargetFramework first (single TFM)
        var targetFramework = project.GetPropertyValue("TargetFramework");
        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            return targetFramework;
        }

        // Try TargetFrameworks (multi-targeting)
        var targetFrameworks = project.GetPropertyValue("TargetFrameworks");
        if (!string.IsNullOrWhiteSpace(targetFrameworks))
        {
            // Return first framework or all if multiple
            var frameworks = targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return frameworks.Length > 0 ? frameworks[0] : targetFrameworks;
        }

        // For .vcxproj files, return null (not applicable)
        return null;
    }

    /// <summary>
    /// Extracts the project type based on file extension.
    /// </summary>
    private static string ExtractProjectType(string projectPath)
    {
        var extension = Path.GetExtension(projectPath).ToLowerInvariant();
        return extension switch
        {
            ".csproj" => "C# Project",
            ".vcxproj" => "C++ Project",
            ".vbproj" => "VB.NET Project",
            ".fsproj" => "F# Project",
            ".vcproj" => "C++ Project (Legacy)",
            ".dbproj" => "Database Project",
            ".shproj" => "Shared Project",
            ".sqlproj" => "SQL Server Project",
            ".pyproj" => "Python Project",
            _ => $"Unknown ({extension})"
        };
    }

    /// <summary>
    /// Extracts the MSBuild ToolsVersion from the project file.
    /// </summary>
    private static string? ExtractToolsVersion(Project project, string projectPath)
    {
        // Try to get ToolsVersion from the project object
        var toolsVersion = project.GetPropertyValue("MSBuildToolsVersion");
        if (!string.IsNullOrWhiteSpace(toolsVersion))
        {
            return toolsVersion;
        }

        // Try to get from ToolsVersion property
        toolsVersion = project.GetPropertyValue("ToolsVersion");
        if (!string.IsNullOrWhiteSpace(toolsVersion))
        {
            return toolsVersion;
        }

        // Try to read directly from XML file
        try
        {
            var xmlContent = File.ReadAllText(projectPath);
            
            // Try to match ToolsVersion attribute in Project tag
            var toolsVersionMatch = System.Text.RegularExpressions.Regex.Match(
                xmlContent,
                @"<Project[^>]*ToolsVersion\s*=\s*""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            
            if (toolsVersionMatch.Success)
            {
                return toolsVersionMatch.Groups[1].Value;
            }

            // Check for SDK-style projects (they use Current or don't specify)
            if (xmlContent.Contains("<Project Sdk="))
            {
                return "Current";
            }
            
            // For .vcxproj files, check for DefaultToolsVersion
            if (projectPath.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase))
            {
                var defaultToolsVersionMatch = System.Text.RegularExpressions.Regex.Match(
                    xmlContent,
                    @"DefaultToolsVersion\s*=\s*""([^""]+)""",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                if (defaultToolsVersionMatch.Success)
                {
                    return defaultToolsVersionMatch.Groups[1].Value;
                }
            }
        }
        catch
        {
            // Ignore errors reading file
        }

        // Default for modern projects
        return "Current";
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

