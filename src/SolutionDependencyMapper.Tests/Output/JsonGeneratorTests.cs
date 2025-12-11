using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Output;
using System.Text.Json;
using Xunit;

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

    [Fact]
    public void Generate_IncludesTargetFramework()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode
                {
                    Path = "A.csproj",
                    Name = "ProjectA",
                    OutputType = "DynamicLibrary",
                    TargetFramework = "net8.0"
                },
                ["B"] = new ProjectNode
                {
                    Path = "B.vcxproj",
                    Name = "ProjectB",
                    OutputType = "StaticLibrary",
                    TargetFramework = null // C++ projects don't have TargetFramework
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
            Assert.Contains("targetFramework", content);
            Assert.Contains("net8.0", content);
            
            // Verify JSON structure
            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;
            Assert.True(root.ValueKind == JsonValueKind.Array);
            
            var projectA = root[0];
            Assert.True(projectA.TryGetProperty("targetFramework", out var tfProperty));
            Assert.Equal("net8.0", tfProperty.GetString());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Generate_WithNET9AndNET10_IncludesTargetFramework()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode
                {
                    Path = "A.csproj",
                    Name = "ProjectA",
                    OutputType = "Library",
                    TargetFramework = "net9.0"
                },
                ["B"] = new ProjectNode
                {
                    Path = "B.csproj",
                    Name = "ProjectB",
                    OutputType = "Library",
                    TargetFramework = "net10.0"
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
            Assert.Contains("net9.0", content);
            Assert.Contains("net10.0", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

