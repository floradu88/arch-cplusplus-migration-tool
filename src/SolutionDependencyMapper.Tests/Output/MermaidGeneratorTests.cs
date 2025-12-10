using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Output;
using Xunit;

namespace SolutionDependencyMapper.Tests.Output;

public class MermaidGeneratorTests
{
    [Fact]
    public void Generate_ValidGraph_CreatesMarkdownFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode
                {
                    Path = "A",
                    Name = "ProjectA",
                    OutputType = "DynamicLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>(),
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            MermaidGenerator.Generate(graph, tempFile);

            // Assert
            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("```mermaid", content);
            Assert.Contains("graph TD", content);
            Assert.Contains("ProjectA", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Generate_IncludesMigrationScores()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode
                {
                    Path = "A",
                    Name = "ProjectA",
                    OutputType = "DynamicLibrary",
                    MigrationScore = 65,
                    MigrationDifficultyLevel = "Very Hard"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>(),
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            MermaidGenerator.Generate(graph, tempFile);

            // Assert
            var content = File.ReadAllText(tempFile);
            Assert.Contains("Migration: 65/100", content);
            Assert.Contains("Very Hard", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Generate_IncludesDependencies()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode { Path = "A", Name = "ProjectA", OutputType = "DynamicLibrary" },
                ["B"] = new ProjectNode { Path = "B", Name = "ProjectB", OutputType = "StaticLibrary" }
            },
            Edges = new List<DependencyEdge>
            {
                new() { FromProject = "A", ToProject = "B" }
            },
            BuildLayers = new List<BuildLayer>(),
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            MermaidGenerator.Generate(graph, tempFile);

            // Assert
            var content = File.ReadAllText(tempFile);
            Assert.Contains("ProjectA", content);
            Assert.Contains("ProjectB", content);
            Assert.Contains("-->", content); // Dependency arrow
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Generate_IncludesBuildLayers()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode { Path = "A", Name = "ProjectA", OutputType = "DynamicLibrary" }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { "A" } }
            },
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            MermaidGenerator.Generate(graph, tempFile);

            // Assert
            var content = File.ReadAllText(tempFile);
            Assert.Contains("## Build Layers", content);
            Assert.Contains("Layer 0", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

