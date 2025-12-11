using Microsoft.Build.Locator;
using SolutionDependencyMapper.Cli;
using SolutionDependencyMapper.Core;
using SolutionDependencyMapper.Output;
using SolutionDependencyMapper.Utils;

namespace SolutionDependencyMapper;

/// <summary>
/// Main entry point for the Solution Dependency Mapper tool.
/// </summary>
class Program
{
    // Global tools context - populated at startup
    private static ToolsContext? _toolsContext;

    static int Main(string[] args)
    {
        if (!CliOptions.TryParse(args, out var options, out var error))
        {
            Console.WriteLine(error);
            PrintUsage();
            return 1;
        }

        // STEP 0: Discover all tools FIRST before everything else
        _toolsContext = DiscoverTools(options);

        if (options.Command == CliCommand.FindTools)
        {
            FindAndPrintTools(options.FindToolsRoot);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.SolutionPath))
        {
            PrintUsage();
            return 1;
        }

        if (!File.Exists(options.SolutionPath))
        {
            Console.WriteLine($"Error: Solution file not found: {options.SolutionPath}");
            return 1;
        }

        // Initialize MSBuildLocator before parsing projects (unless --assume-vs-env)
        if (!MsBuildBootstrapper.EnsureRegistered(options.AssumeVsEnv, _toolsContext))
        {
            return 1;
        }

        return RunAnalysis(options.SolutionPath, options.AssumeVsEnv, options.AutoInstallPackages);
    }

    private static ToolsContext DiscoverTools(CliOptions options)
    {
        Console.WriteLine("Discovering build tools...");

        string? solutionRoot = null;
        if (options.Command == CliCommand.AnalyzeSolution && !string.IsNullOrWhiteSpace(options.SolutionPath))
        {
            // Even if the file doesn't exist yet, this gives ToolFinder a good starting point.
            solutionRoot = Path.GetDirectoryName(Path.GetFullPath(options.SolutionPath));
        }

        var ctx = new ToolsContext
        {
            AllTools = ToolFinder.FindAllTools(solutionRoot)
        };

        var toolCount = ctx.AllTools.Values.Sum(v => v.Count);
        Console.WriteLine($"Found {toolCount} tool instances across {ctx.AllTools.Count} tool types.");

        if (ctx.HasTool("msbuild.exe"))
        {
            Console.WriteLine($"  MSBuild: {ctx.GetMSBuildPath()}");
        }
        if (ctx.HasTool("cmake.exe"))
        {
            Console.WriteLine($"  CMake: {ctx.GetCmakePath()}");
        }
        Console.WriteLine();
        return ctx;
    }

    private static int RunAnalysis(string solutionPath, bool assumeVsEnv, bool autoInstallPackages)
    {
        try
        {
            if (!assumeVsEnv && !MSBuildLocator.IsRegistered)
            {
                Console.WriteLine("❌ Error: MSBuildLocator is not registered. Cannot parse project files.");
                Console.WriteLine("Or use --assume-vs-env flag if running from VS Developer Command Prompt.");
                return 1;
            }

            Console.WriteLine($"Loading solution: {solutionPath}");

            var solutionInfo = SolutionLoader.ExtractSolutionInfo(solutionPath);
            var projectPaths = solutionInfo.Projects.Select(p => p.FullPath).ToList();
            Console.WriteLine($"Found {projectPaths.Count} projects.");

            if (projectPaths.Count == 0)
            {
                Console.WriteLine("Warning: No projects found in solution.");
                return 1;
            }

            Console.WriteLine("\nParsing projects...");
            if (!autoInstallPackages)
            {
                Console.WriteLine("  Note: Automatic package installation is disabled (--no-auto-install-packages)");
            }

            var projects = new List<Models.ProjectNode>();
            var failedProjects = new List<(string Path, string Error)>();

            foreach (var projectPath in projectPaths)
            {
                try
                {
                    Console.WriteLine($"  Parsing: {Path.GetFileName(projectPath)}");
                    var project = ProjectParser.ParseProject(projectPath, assumeVsEnv, maxRetries: 1, autoInstallPackages: autoInstallPackages);
                    if (project != null)
                    {
                        var match = solutionInfo.Projects.FirstOrDefault(p => string.Equals(p.FullPath, projectPath, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            project.SolutionProjectGuid = match.ProjectGuid;
                            project.SolutionConfigurationMappings = match.ConfigurationMappings;
                        }

                        projects.Add(project);
                        Console.WriteLine($"    ✓ Successfully parsed: {Path.GetFileName(projectPath)}");
                    }
                    else
                    {
                        failedProjects.Add((projectPath, "Parsing returned null (see errors above)"));
                        Console.WriteLine($"    ✗ Failed to parse: {Path.GetFileName(projectPath)}");
                    }
                }
                catch (Exception ex)
                {
                    failedProjects.Add((projectPath, ex.Message));
                    Console.WriteLine($"    ✗ Error parsing {Path.GetFileName(projectPath)}: {ex.Message}");
                    Console.WriteLine("      (Continuing with remaining projects...)");
                }
            }

            PrintParsingSummary(projects.Count, failedProjects);

            if (projects.Count == 0)
            {
                Console.WriteLine("\n⚠️  Warning: No projects were successfully parsed. Cannot generate summary or outputs.");
                return 1;
            }

            PrintSolutionSummary(projects, solutionPath);

            Console.WriteLine("\nBuilding dependency graph...");
            var graph = DependencyGraphBuilder.BuildGraph(projects);
            Console.WriteLine($"  Nodes: {graph.Nodes.Count}");
            Console.WriteLine($"  Edges: {graph.Edges.Count}");
            Console.WriteLine($"  Build Layers: {graph.BuildLayers.Count}");
            if (graph.Cycles.Count > 0)
            {
                Console.WriteLine($"  ⚠️  Circular Dependencies: {graph.Cycles.Count}");
            }

            var outputDir = Path.Combine(Path.GetDirectoryName(solutionPath) ?? ".", "output");
            Directory.CreateDirectory(outputDir);

            Console.WriteLine("\nGenerating outputs...");
            var jsonPath = Path.Combine(outputDir, "dependency-tree.json");
            JsonGenerator.Generate(graph, jsonPath);
            Console.WriteLine($"  ✓ Generated: {jsonPath}");

            var mermaidPath = Path.Combine(outputDir, "dependency-graph.md");
            MermaidGenerator.Generate(graph, mermaidPath);
            Console.WriteLine($"  ✓ Generated: {mermaidPath}");

            var drawioPath = Path.Combine(outputDir, "dependency-graph.drawio");
            DrawioGenerator.Generate(graph, drawioPath);
            Console.WriteLine($"  ✓ Generated: {drawioPath}");

            Console.WriteLine("\nGenerating build scripts...");
            BuildScriptGenerator.GenerateAll(graph, solutionPath, outputDir, "Release", "x64", _toolsContext);
            Console.WriteLine($"  ✓ Generated: {Path.Combine(outputDir, "build-layers.json")}");
            Console.WriteLine($"  ✓ Generated: {Path.Combine(outputDir, "build.ps1")}");
            Console.WriteLine($"  ✓ Generated: {Path.Combine(outputDir, "build.bat")}");
            Console.WriteLine($"  ✓ Generated: {Path.Combine(outputDir, "build.sh")}");

            Console.WriteLine("\n✓ Analysis complete!");
            Console.WriteLine($"\nOutput directory: {outputDir}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static void PrintParsingSummary(int parsedCount, List<(string Path, string Error)> failedProjects)
    {
        Console.WriteLine("\nParsing Summary:");
        Console.WriteLine($"  ✓ Successfully parsed: {parsedCount} project(s)");
        if (failedProjects.Count == 0)
            return;

        Console.WriteLine($"  ✗ Failed to parse: {failedProjects.Count} project(s)");
        Console.WriteLine("\nFailed Projects:");
        foreach (var (path, error) in failedProjects)
        {
            Console.WriteLine($"  - {Path.GetFileName(path)}: {error}");
        }
    }

    private static void FindAndPrintTools(string? projectRoot)
    {
        Console.WriteLine("Solution Dependency Mapper - Tool Finder");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        Console.WriteLine("Searching for Visual Studio tools, CMake, and other C++ build tools...");
        Console.WriteLine();

        if (!string.IsNullOrEmpty(projectRoot))
        {
            Console.WriteLine($"Project root: {Path.GetFullPath(projectRoot)}");
            Console.WriteLine();
        }

        var tools = ToolFinder.FindAllTools(projectRoot);
        ToolFinder.PrintFoundTools(tools);

        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"Total tools found: {tools.Values.Sum(v => v.Count)}");
        Console.WriteLine($"Unique tool types: {tools.Count}");
    }

    private static void PrintSolutionSummary(List<Models.ProjectNode> projects, string solutionPath)
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("SOLUTION SUMMARY REPORT");
        Console.WriteLine(new string('=', 70));
        Console.WriteLine($"Solution: {Path.GetFileName(solutionPath)}");
        Console.WriteLine($"Total Projects: {projects.Count}");
        Console.WriteLine();

        // Reference totals (structured)
        var totalNuGet = projects.Sum(p => p.NuGetPackageReferences.Count);
        var totalFrameworkRefs = projects.Sum(p => p.FrameworkReferences.Count);
        var totalAssemblyRefs = projects.Sum(p => p.AssemblyReferences.Count);
        var totalComRefs = projects.Sum(p => p.ComReferences.Count);
        var totalAnalyzers = projects.Sum(p => p.AnalyzerReferences.Count);
        var totalNativeLibs = projects.Sum(p => p.NativeLibraries.Count);
        var totalDelayLoadDlls = projects.Sum(p => p.NativeDelayLoadDlls.Count);
        var totalIncludeDirs = projects.Sum(p => p.IncludeDirectories.Count);
        var totalHeaders = projects.Sum(p => p.HeaderFiles.Count);
        var totalValidationIssues = projects.Sum(p => p.ReferenceValidationIssues.Count);

        Console.WriteLine("Reference Totals:");
        Console.WriteLine($"  NuGet packages: {totalNuGet}");
        Console.WriteLine($"  Framework references: {totalFrameworkRefs}");
        Console.WriteLine($"  Assembly references: {totalAssemblyRefs}");
        Console.WriteLine($"  COM references: {totalComRefs}");
        Console.WriteLine($"  Analyzers: {totalAnalyzers}");
        Console.WriteLine($"  Native libraries: {totalNativeLibs}");
        Console.WriteLine($"  Delay-load DLLs: {totalDelayLoadDlls}");
        Console.WriteLine($"  Include directories: {totalIncludeDirs}");
        Console.WriteLine($"  Header files: {totalHeaders}");
        Console.WriteLine($"  Missing/invalid reference paths: {totalValidationIssues}");
        Console.WriteLine();

        // Build matrix summary
        var allConfigs = projects.SelectMany(p => p.Configurations).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var allPlatforms = projects.SelectMany(p => p.Platforms).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        Console.WriteLine("Build Matrix:");
        Console.WriteLine($"  Configurations: {(allConfigs.Count == 0 ? "N/A" : string.Join(", ", allConfigs))}");
        Console.WriteLine($"  Platforms: {(allPlatforms.Count == 0 ? "N/A" : string.Join(", ", allPlatforms))}");
        Console.WriteLine();

        var solutionPairs = projects
            .SelectMany(p => p.SolutionConfigurationMappings)
            .Select(m => m.Solution.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (solutionPairs.Count > 0)
        {
            Console.WriteLine("Solution Build Matrix (from .sln ProjectConfigurationPlatforms):");
            Console.WriteLine($"  Solution Configuration|Platform pairs: {string.Join(", ", solutionPairs)}");
            Console.WriteLine();
        }

        // Group by project type
        var projectsByType = projects
            .GroupBy(p => p.ProjectType ?? "Unknown")
            .OrderBy(g => g.Key);

        Console.WriteLine("Project Types:");
        foreach (var group in projectsByType)
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} project(s)");
        }
        Console.WriteLine();

        // Group by ToolsVersion
        var projectsByToolsVersion = projects
            .Where(p => !string.IsNullOrWhiteSpace(p.ToolsVersion))
            .GroupBy(p => p.ToolsVersion!)
            .OrderBy(g => g.Key);

        if (projectsByToolsVersion.Any())
        {
            Console.WriteLine("ToolsVersion Distribution:");
            foreach (var group in projectsByToolsVersion)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()} project(s)");
            }
            Console.WriteLine();
        }

        // Group by output type
        var projectsByOutputType = projects
            .GroupBy(p => p.OutputType)
            .OrderBy(g => g.Key);

        Console.WriteLine("Output Types:");
        foreach (var group in projectsByOutputType)
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} project(s)");
        }
        Console.WriteLine();

        // Detailed project list
        Console.WriteLine("Project Details:");
        Console.WriteLine(new string('-', 70));
        Console.WriteLine($"{"Project Name",-30} {"Type",-15} {"ToolsVersion",-12} {"Output",-10}");
        Console.WriteLine(new string('-', 70));

        foreach (var project in projects.OrderBy(p => p.Name))
        {
            var projectType = project.ProjectType ?? "Unknown";
            var toolsVersion = project.ToolsVersion ?? "N/A";
            var outputType = project.OutputType;
            
            Console.WriteLine($"{project.Name,-30} {projectType,-15} {toolsVersion,-12} {outputType,-10}");
        }
        Console.WriteLine(new string('-', 70));
        Console.WriteLine();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Solution Dependency Mapper");
        Console.WriteLine("==========================");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  SolutionDependencyMapper <path-to-solution.sln> [--assume-vs-env]");
        Console.WriteLine("  SolutionDependencyMapper --find-tools [project-root]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  <path-to-solution.sln>  Analyze solution and generate outputs");
        Console.WriteLine("  --find-tools            Find all Visual Studio tools, CMake, and C++ tools");
        Console.WriteLine("                          Optional: specify project root directory to search");
        Console.WriteLine();
        Console.WriteLine("Flags:");
        Console.WriteLine("  --assume-vs-env         Assume VS Developer Command Prompt environment is configured");
        Console.WriteLine("                          Skips MSBuildLocator registration, uses MSBuild/dotnet directly");
        Console.WriteLine("                          Use this when running from VS2022 Developer Command Prompt");
        Console.WriteLine();
        Console.WriteLine("  --auto-install-packages Automatically install missing Microsoft.Build packages (default: enabled)");
        Console.WriteLine("                          When a project fails due to missing packages, automatically installs");
        Console.WriteLine("                          Microsoft.Build 15.1.548 and related packages, then retries parsing");
        Console.WriteLine();
        Console.WriteLine("  --no-auto-install-packages  Disable automatic package installation");
        Console.WriteLine("                          Projects with missing packages will fail without attempting to fix them");
        Console.WriteLine();
        Console.WriteLine("This tool analyzes a Visual Studio solution and generates:");
        Console.WriteLine("  - dependency-tree.json (machine-readable dependency data)");
        Console.WriteLine("  - dependency-graph.md (MermaidJS diagram)");
        Console.WriteLine("  - dependency-graph.drawio (Draw.io diagram)");
        Console.WriteLine("  - build-layers.json (build layer structure)");
        Console.WriteLine("  - build.ps1 (PowerShell build script)");
        Console.WriteLine("  - build.bat (Batch build script)");
        Console.WriteLine("  - build.sh (Shell build script for Linux/macOS)");
        Console.WriteLine();
        Console.WriteLine("Output files are written to: <solution-directory>/output/");
        Console.WriteLine();
        Console.WriteLine("Tool Finder searches in:");
        Console.WriteLine("  - Project root directory (if specified)");
        Console.WriteLine("  - PATH environment variable");
        Console.WriteLine("  - Common Windows installation locations");
        Console.WriteLine("  - Visual Studio directories (using vswhere.exe)");
    }
}

