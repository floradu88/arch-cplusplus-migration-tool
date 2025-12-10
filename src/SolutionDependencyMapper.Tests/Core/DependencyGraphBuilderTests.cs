using SolutionDependencyMapper.Core;
using SolutionDependencyMapper.Models;
using Xunit;

namespace SolutionDependencyMapper.Tests.Core;

public class DependencyGraphBuilderTests
{
    [Fact]
    public void BuildGraph_CreatesGraphWithNodes()
    {
        // Arrange
        var projects = new List<ProjectNode>
        {
            new() { Path = "A", Name = "ProjectA", OutputType = "DynamicLibrary" },
            new() { Path = "B", Name = "ProjectB", OutputType = "StaticLibrary" }
        };

        // Act
        var graph = DependencyGraphBuilder.BuildGraph(projects);

        // Assert
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Contains("A", graph.Nodes.Keys);
        Assert.Contains("B", graph.Nodes.Keys);
    }

    [Fact]
    public void BuildGraph_CreatesEdgesFromDependencies()
    {
        // Arrange
        var projects = new List<ProjectNode>
        {
            new()
            {
                Path = "A",
                Name = "ProjectA",
                OutputType = "DynamicLibrary",
                ProjectDependencies = new List<string> { "B" }
            },
            new() { Path = "B", Name = "ProjectB", OutputType = "StaticLibrary" }
        };

        // Act
        var graph = DependencyGraphBuilder.BuildGraph(projects);

        // Assert
        Assert.Single(graph.Edges);
        Assert.Equal("A", graph.Edges[0].FromProject);
        Assert.Equal("B", graph.Edges[0].ToProject);
    }

    [Fact]
    public void BuildGraph_CalculatesMigrationScores()
    {
        // Arrange
        var projects = new List<ProjectNode>
        {
            new()
            {
                Path = "A.csproj",
                Name = "ProjectA",
                OutputType = "DynamicLibrary"
            }
        };

        // Act
        var graph = DependencyGraphBuilder.BuildGraph(projects);

        // Assert
        Assert.NotNull(graph.Nodes["A.csproj"].MigrationScore);
        Assert.NotNull(graph.Nodes["A.csproj"].MigrationDifficultyLevel);
    }

    [Fact]
    public void BuildGraph_DetectsCycles()
    {
        // Arrange
        var projects = new List<ProjectNode>
        {
            new()
            {
                Path = "A",
                Name = "ProjectA",
                OutputType = "DynamicLibrary",
                ProjectDependencies = new List<string> { "B" }
            },
            new()
            {
                Path = "B",
                Name = "ProjectB",
                OutputType = "StaticLibrary",
                ProjectDependencies = new List<string> { "A" }
            }
        };

        // Act
        var graph = DependencyGraphBuilder.BuildGraph(projects);

        // Assert
        Assert.NotEmpty(graph.Cycles);
    }

    [Fact]
    public void BuildGraph_CreatesBuildLayers()
    {
        // Arrange
        var projects = new List<ProjectNode>
        {
            new()
            {
                Path = "A",
                Name = "ProjectA",
                OutputType = "StaticLibrary"
            },
            new()
            {
                Path = "B",
                Name = "ProjectB",
                OutputType = "DynamicLibrary",
                ProjectDependencies = new List<string> { "A" }
            }
        };

        // Act
        var graph = DependencyGraphBuilder.BuildGraph(projects);

        // Assert
        Assert.NotEmpty(graph.BuildLayers);
        Assert.True(graph.BuildLayers.Count >= 2);
    }

    [Fact]
    public void BuildGraph_EmptyProjectList_CreatesEmptyGraph()
    {
        // Arrange
        var projects = new List<ProjectNode>();

        // Act
        var graph = DependencyGraphBuilder.BuildGraph(projects);

        // Assert
        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Edges);
        Assert.Empty(graph.BuildLayers);
        Assert.Empty(graph.Cycles);
    }
}

