using SolutionDependencyMapper.Core;
using SolutionDependencyMapper.Output;
using SolutionDependencyMapper.Models;
using Xunit;

namespace SolutionDependencyMapper.Tests.Integration;

public class EndToEndTests
{
    [Fact]
    public void FullWorkflow_SimpleSolution_GeneratesAllOutputs()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var solutionPath = Path.Combine(tempDir, "TestSolution.sln");
        var project1Path = Path.Combine(tempDir, "Project1.vcxproj");
        var project2Path = Path.Combine(tempDir, "Project2.csproj");
        var outputDir = Path.Combine(tempDir, "output");

        // Create test solution
        var solutionContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project1"", ""Project1.vcxproj"", ""{11111111-1111-1111-1111-111111111111}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project2"", ""Project2.csproj"", ""{22222222-2222-2222-2222-222222222222}""
EndProject
";
        File.WriteAllText(solutionPath, solutionContent);
        File.WriteAllText(project1Path, "<Project></Project>");
        File.WriteAllText(project2Path, "<Project></Project>");

        try
        {
            // Act - Simulate the full workflow
            var projectPaths = SolutionLoader.ExtractProjectsFromSolution(solutionPath);
            var projects = new List<ProjectNode>();
            
            // For integration test, we'll create mock projects since we can't actually parse without MSBuild
            foreach (var path in projectPaths)
            {
                projects.Add(new ProjectNode
                {
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    OutputType = path.EndsWith(".vcxproj") ? "StaticLibrary" : "DynamicLibrary",
                    ProjectDependencies = new List<string>(),
                    ExternalDependencies = new List<string>()
                });
            }

            var graph = DependencyGraphBuilder.BuildGraph(projects);
            Directory.CreateDirectory(outputDir);

            JsonGenerator.Generate(graph, Path.Combine(outputDir, "dependency-tree.json"));
            MermaidGenerator.Generate(graph, Path.Combine(outputDir, "dependency-graph.md"));
            DrawioGenerator.Generate(graph, Path.Combine(outputDir, "dependency-graph.drawio"));
            BuildScriptGenerator.GenerateAll(graph, solutionPath, outputDir, "Release", "x64");

            // Assert
            Assert.True(File.Exists(Path.Combine(outputDir, "dependency-tree.json")));
            Assert.True(File.Exists(Path.Combine(outputDir, "dependency-graph.md")));
            Assert.True(File.Exists(Path.Combine(outputDir, "dependency-graph.drawio")));
            Assert.True(File.Exists(Path.Combine(outputDir, "build-layers.json")));
            Assert.True(File.Exists(Path.Combine(outputDir, "build.ps1")));
            Assert.True(File.Exists(Path.Combine(outputDir, "build.bat")));
            Assert.True(File.Exists(Path.Combine(outputDir, "build.sh")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void FullWorkflow_WithDependencies_CreatesCorrectGraph()
    {
        // Arrange
        var projects = new List<ProjectNode>
        {
            new()
            {
                Path = "Utils.vcxproj",
                Name = "Utils",
                OutputType = "StaticLibrary",
                ProjectDependencies = new List<string>()
            },
            new()
            {
                Path = "Core.vcxproj",
                Name = "Core",
                OutputType = "StaticLibrary",
                ProjectDependencies = new List<string> { "Utils.vcxproj" }
            },
            new()
            {
                Path = "App.vcxproj",
                Name = "App",
                OutputType = "Exe",
                ProjectDependencies = new List<string> { "Core.vcxproj" }
            }
        };

        // Act
        var graph = DependencyGraphBuilder.BuildGraph(projects);

        // Assert
        Assert.Equal(3, graph.Nodes.Count);
        Assert.Equal(2, graph.Edges.Count);
        Assert.True(graph.BuildLayers.Count >= 3); // Should have at least 3 layers
        Assert.Empty(graph.Cycles); // No cycles in this structure

        // Verify build order
        var layer0 = graph.BuildLayers.FirstOrDefault(l => l.LayerNumber == 0);
        Assert.NotNull(layer0);
        Assert.Contains("Utils.vcxproj", layer0.ProjectPaths);
    }
}

