using Microsoft.Build.Locator;
using SolutionDependencyMapper.Core;
using SolutionDependencyMapper.Output;

namespace SolutionDependencyMapper;

/// <summary>
/// Main entry point for the Solution Dependency Mapper tool.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        // Initialize MSBuildLocator FIRST, before any MSBuild types are used
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

            // If still no instances, try to find MSBuild in common paths
            if (instances.Count == 0)
            {
                var msbuildPaths = new[]
                {
                    @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
                    @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
                    @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe",
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"
                };

                var foundPath = msbuildPaths.FirstOrDefault(File.Exists);
                if (foundPath != null)
                {
                    Console.WriteLine($"Warning: MSBuildLocator could not find Visual Studio instances.");
                    Console.WriteLine($"Found MSBuild at: {foundPath}");
                    Console.WriteLine($"However, MSBuildLocator requires Visual Studio to be properly registered.");
                    Console.WriteLine();
                }

                Console.WriteLine("Error: No MSBuild instances found.");
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

            // Use the highest version available
            var instance = instances.OrderByDescending(i => i.Version).First();
            MSBuildLocator.RegisterInstance(instance);
            Console.WriteLine($"Using MSBuild from: {instance.MSBuildPath}");
            Console.WriteLine($"MSBuild Version: {instance.Version}");
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
            BuildScriptGenerator.GenerateAll(graph, solutionPath, outputDir, "Release", "x64");
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

    private static void PrintUsage()
    {
        Console.WriteLine("Solution Dependency Mapper");
        Console.WriteLine("==========================");
        Console.WriteLine();
        Console.WriteLine("Usage: SolutionDependencyMapper <path-to-solution.sln>");
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
    }
}

