using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Output;
using Xunit;
using System.Text.Json;

namespace SolutionDependencyMapper.Tests.Output;

public class JsonGeneratorTests
{
    [Fact]
    public void Generate_ValidGraph_CreatesValidJsonFile()
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
                    MigrationScore = 25,
                    MigrationDifficultyLevel = "Moderate"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>(),
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            JsonGenerator.Generate(graph, tempFile);

            // Assert
            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.NotEmpty(content);

            // Verify it's valid JSON
            var projects = JsonSerializer.Deserialize<List<object>>(content);
            Assert.NotNull(projects);
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
                    MigrationScore = 45,
                    MigrationDifficultyLevel = "Hard"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>(),
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            JsonGenerator.Generate(graph, tempFile);

            // Assert
            var content = File.ReadAllText(tempFile);
            Assert.Contains("migrationScore", content);
            Assert.Contains("45", content);
            Assert.Contains("migrationDifficultyLevel", content);
            Assert.Contains("Hard", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Generate_EmptyGraph_CreatesEmptyArray()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>(),
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>(),
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            JsonGenerator.Generate(graph, tempFile);

            // Assert
            var content = File.ReadAllText(tempFile);
            var projects = JsonSerializer.Deserialize<List<object>>(content);
            Assert.NotNull(projects);
            Assert.Empty(projects);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

