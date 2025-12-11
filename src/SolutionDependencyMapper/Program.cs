using Microsoft.Build.Locator;
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
        // STEP 0: Discover all tools FIRST before everything else
        Console.WriteLine("Discovering build tools...");
        var solutionRoot = args.Length > 0 && File.Exists(args[0]) 
            ? Path.GetDirectoryName(Path.GetFullPath(args[0])) 
            : null;
        
        _toolsContext = new ToolsContext
        {
            AllTools = ToolFinder.FindAllTools(solutionRoot)
        };

        var toolCount = _toolsContext.AllTools.Values.Sum(v => v.Count);
        Console.WriteLine($"Found {toolCount} tool instances across {_toolsContext.AllTools.Count} tool types.");
        
        // Show key tools found
        if (_toolsContext.HasTool("msbuild.exe"))
        {
            var msbuildPath = _toolsContext.GetMSBuildPath();
            Console.WriteLine($"  MSBuild: {msbuildPath}");
        }
        if (_toolsContext.HasTool("cmake.exe"))
        {
            var cmakePath = _toolsContext.GetCmakePath();
            Console.WriteLine($"  CMake: {cmakePath}");
        }
        Console.WriteLine();

        // Check for special commands BEFORE MSBuildLocator initialization
        if (args.Length > 0)
        {
            if (args[0] == "--find-tools" || args[0] == "--tools" || args[0] == "-t")
            {
                FindAndPrintTools(args.Length > 1 ? args[1] : null);
                return 0;
            }
        }

        // Initialize MSBuildLocator using discovered tools
        // (Only needed for solution analysis, not for --find-tools)
        if (!MSBuildLocator.IsRegistered)
        {
            // Try to find MSBuild instances with different query options
            var instances = MSBuildLocator.QueryVisualStudioInstances(
                VisualStudioInstanceQueryOptions.Default
            ).ToList();

            // If no instances found, try including prerelease versions
            if (instances.Count == 0)
            {
                instances = MSBuildLocator.QueryVisualStudioInstances(
                    VisualStudioInstanceQueryOptions.Default
                ).ToList();
            }

            // If still no instances, check if ToolFinder found MSBuild
            if (instances.Count == 0 && _toolsContext != null && _toolsContext.HasTool("msbuild.exe"))
            {
                var msbuildPath = _toolsContext.GetMSBuildPath();
                if (msbuildPath != null && File.Exists(msbuildPath))
                {
                    Console.WriteLine("⚠️  Warning: MSBuildLocator could not find Visual Studio instances.");
                    Console.WriteLine($"   However, ToolFinder found MSBuild at: {msbuildPath}");
                    Console.WriteLine();
                    Console.WriteLine("   MSBuildLocator requires Visual Studio to be properly registered in the system.");
                    Console.WriteLine("   The tool can still generate build scripts, but project parsing may fail.");
                    Console.WriteLine();
                    Console.WriteLine("   To fix this:");
                    Console.WriteLine("   1. Ensure Visual Studio or Build Tools are properly installed");
                    Console.WriteLine("   2. Try running from 'Developer Command Prompt for VS'");
                    Console.WriteLine("   3. Repair Visual Studio installation if needed");
                    Console.WriteLine();
                    Console.WriteLine("   Continuing anyway (build scripts will use discovered MSBuild path)...");
                    Console.WriteLine();
                    
                    // Don't return error - allow the tool to continue for build script generation
                    // Project parsing will fail later if MSBuildLocator is truly needed
                }
            }

            if (instances.Count == 0)
            {
                // If ToolFinder also didn't find MSBuild, show full error
                if (_toolsContext == null || !_toolsContext.HasTool("msbuild.exe"))
                {
                    Console.WriteLine("❌ Error: No MSBuild instances found.");
                    Console.WriteLine();
                    Console.WriteLine("Possible solutions:");
                    Console.WriteLine("1. Install Visual Studio Build Tools or Visual Studio");
                    Console.WriteLine("   Download: https://visualstudio.microsoft.com/downloads/");
                    Console.WriteLine();
                    Console.WriteLine("2. For Build Tools, ensure 'MSBuild' workload is installed");
                    Console.WriteLine();
                    Console.WriteLine("3. Try running 'Developer Command Prompt for VS' or 'Developer PowerShell for VS'");
                    Console.WriteLine("   These set up the environment correctly for MSBuild");
                    Console.WriteLine();
                    Console.WriteLine("4. If Visual Studio is installed, try repairing the installation");
                    Console.WriteLine("   (Visual Studio Installer > Modify > Repair)");
                    return 1;
                }
                else
                {
                    // ToolFinder found MSBuild but MSBuildLocator didn't
                    // Continue with warning (already shown above)
                }
            }

            // Use the highest version available
            var instance = instances.OrderByDescending(i => i.Version).First();
            MSBuildLocator.RegisterInstance(instance);
            Console.WriteLine($"✓ Using MSBuild from: {instance.MSBuildPath}");
            Console.WriteLine($"  MSBuild Version: {instance.Version}");
        }

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var solutionPath = args[0];
        if (!File.Exists(solutionPath))
        {
            Console.WriteLine($"Error: Solution file not found: {solutionPath}");
            return 1;
        }

        try
        {
            // Check if MSBuildLocator is registered (required for project parsing)
            if (!MSBuildLocator.IsRegistered)
            {
                Console.WriteLine("❌ Error: MSBuildLocator is not registered. Cannot parse project files.");
                Console.WriteLine();
                if (_toolsContext != null && _toolsContext.HasTool("msbuild.exe"))
                {
                    Console.WriteLine("Note: MSBuild was found via ToolFinder, but MSBuildLocator requires");
                    Console.WriteLine("      Visual Studio to be properly registered in the system.");
                    Console.WriteLine();
                }
                Console.WriteLine("Please ensure Visual Studio or Build Tools are properly installed and registered.");
                return 1;
            }

            Console.WriteLine($"Loading solution: {solutionPath}");

            // Step 1: Extract project paths from solution
            var projectPaths = SolutionLoader.ExtractProjectsFromSolution(solutionPath);
            Console.WriteLine($"Found {projectPaths.Count} projects.");

            if (projectPaths.Count == 0)
            {
                Console.WriteLine("Warning: No projects found in solution.");
                return 1;
            }

            // Step 2: Parse each project
            Console.WriteLine("\nParsing projects...");
            var projects = new List<Models.ProjectNode>();
            foreach (var projectPath in projectPaths)
            {
                Console.WriteLine($"  Parsing: {Path.GetFileName(projectPath)}");
                var project = ProjectParser.ParseProject(projectPath);
                if (project != null)
                {
                    projects.Add(project);
                }
            }

            Console.WriteLine($"Successfully parsed {projects.Count} projects.");

            // Step 3: Build dependency graph
            Console.WriteLine("\nBuilding dependency graph...");
            var graph = DependencyGraphBuilder.BuildGraph(projects);
            Console.WriteLine($"  Nodes: {graph.Nodes.Count}");
            Console.WriteLine($"  Edges: {graph.Edges.Count}");
            Console.WriteLine($"  Build Layers: {graph.BuildLayers.Count}");

            if (graph.Cycles.Count > 0)
            {
                Console.WriteLine($"  ⚠️  Circular Dependencies: {graph.Cycles.Count}");
            }

            // Step 4: Generate outputs
            var outputDir = Path.Combine(Path.GetDirectoryName(solutionPath) ?? ".", "output");
            Directory.CreateDirectory(outputDir);

            Console.WriteLine("\nGenerating outputs...");

            // JSON output
            var jsonPath = Path.Combine(outputDir, "dependency-tree.json");
            JsonGenerator.Generate(graph, jsonPath);
            Console.WriteLine($"  ✓ Generated: {jsonPath}");

            // MermaidJS output
            var mermaidPath = Path.Combine(outputDir, "dependency-graph.md");
            MermaidGenerator.Generate(graph, mermaidPath);
            Console.WriteLine($"  ✓ Generated: {mermaidPath}");

            // Draw.io output
            var drawioPath = Path.Combine(outputDir, "dependency-graph.drawio");
            DrawioGenerator.Generate(graph, drawioPath);
            Console.WriteLine($"  ✓ Generated: {drawioPath}");

            // Build scripts output (addon feature)
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

    private static void PrintUsage()
    {
        Console.WriteLine("Solution Dependency Mapper");
        Console.WriteLine("==========================");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  SolutionDependencyMapper <path-to-solution.sln>");
        Console.WriteLine("  SolutionDependencyMapper --find-tools [project-root]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  <path-to-solution.sln>  Analyze solution and generate outputs");
        Console.WriteLine("  --find-tools            Find all Visual Studio tools, CMake, and C++ tools");
        Console.WriteLine("                          Optional: specify project root directory to search");
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

