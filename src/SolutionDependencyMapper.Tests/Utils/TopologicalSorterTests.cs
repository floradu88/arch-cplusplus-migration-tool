using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Utils;
using Xunit;

namespace SolutionDependencyMapper.Tests.Utils;

public class TopologicalSorterTests
{
    [Fact]
    public void SortIntoLayers_SimpleDependencyChain_CreatesCorrectLayers()
    {
        // Arrange
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode { Path = "A", Name = "A" },
                ["B"] = new ProjectNode { Path = "B", Name = "B" },
                ["C"] = new ProjectNode { Path = "C", Name = "C" }
            },
            Edges = new List<DependencyEdge>
            {
                new() { FromProject = "C", ToProject = "B" },
                new() { FromProject = "B", ToProject = "A" }
            }
        };

        // Act
        var layers = TopologicalSorter.SortIntoLayers(graph);

        // Assert
        Assert.Equal(3, layers.Count);
        Assert.Equal(0, layers[0].LayerNumber);
        Assert.Contains("A", layers[0].ProjectPaths);
        Assert.Equal(1, layers[1].LayerNumber);
        Assert.Contains("B", layers[1].ProjectPaths);
        Assert.Equal(2, layers[2].LayerNumber);
        Assert.Contains("C", layers[2].ProjectPaths);
    }

    [Fact]
    public void SortIntoLayers_IndependentProjects_AllInLayerZero()
    {
        // Arrange
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode { Path = "A", Name = "A" },
                ["B"] = new ProjectNode { Path = "B", Name = "B" },
                ["C"] = new ProjectNode { Path = "C", Name = "C" }
            },
            Edges = new List<DependencyEdge>()
        };

        // Act
        var layers = TopologicalSorter.SortIntoLayers(graph);

        // Assert
        Assert.Single(layers);
        Assert.Equal(3, layers[0].ProjectPaths.Count);
        Assert.Contains("A", layers[0].ProjectPaths);
        Assert.Contains("B", layers[0].ProjectPaths);
        Assert.Contains("C", layers[0].ProjectPaths);
    }

    [Fact]
    public void SortIntoLayers_ParallelDependencies_CreatesCorrectLayers()
    {
        // Arrange
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode { Path = "A", Name = "A" },
                ["B"] = new ProjectNode { Path = "B", Name = "B" },
                ["C"] = new ProjectNode { Path = "C", Name = "C" }
            },
            Edges = new List<DependencyEdge>
            {
                new() { FromProject = "C", ToProject = "A" },
                new() { FromProject = "C", ToProject = "B" }
            }
        };

        // Act
        var layers = TopologicalSorter.SortIntoLayers(graph);

        // Assert
        Assert.Equal(2, layers.Count);
        Assert.Equal(2, layers[0].ProjectPaths.Count); // A and B in layer 0
        Assert.Contains("A", layers[0].ProjectPaths);
        Assert.Contains("B", layers[0].ProjectPaths);
        Assert.Single(layers[1].ProjectPaths); // C in layer 1
        Assert.Contains("C", layers[1].ProjectPaths);
    }

    [Fact]
    public void SortIntoLayers_EmptyGraph_ReturnsEmptyList()
    {
        // Arrange
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>(),
            Edges = new List<DependencyEdge>()
        };

        // Act
        var layers = TopologicalSorter.SortIntoLayers(graph);

        // Assert
        Assert.Empty(layers);
    }
}

