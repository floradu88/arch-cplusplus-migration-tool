using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Utils;
using Xunit;

namespace SolutionDependencyMapper.Tests.Utils;

public class CycleDetectorTests
{
    [Fact]
    public void DetectCycles_NoCycles_ReturnsEmptyList()
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
        var cycles = CycleDetector.DetectCycles(graph);

        // Assert
        Assert.Empty(cycles);
    }

    [Fact]
    public void DetectCycles_SimpleCycle_DetectsCycle()
    {
        // Arrange
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode { Path = "A", Name = "A" },
                ["B"] = new ProjectNode { Path = "B", Name = "B" }
            },
            Edges = new List<DependencyEdge>
            {
                new() { FromProject = "A", ToProject = "B" },
                new() { FromProject = "B", ToProject = "A" }
            }
        };

        // Act
        var cycles = CycleDetector.DetectCycles(graph);

        // Assert
        Assert.NotEmpty(cycles);
        Assert.Contains(cycles, c => c.Contains("A") && c.Contains("B"));
    }

    [Fact]
    public void DetectCycles_ThreeNodeCycle_DetectsCycle()
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
                new() { FromProject = "A", ToProject = "B" },
                new() { FromProject = "B", ToProject = "C" },
                new() { FromProject = "C", ToProject = "A" }
            }
        };

        // Act
        var cycles = CycleDetector.DetectCycles(graph);

        // Assert
        Assert.NotEmpty(cycles);
    }

    [Fact]
    public void DetectCycles_EmptyGraph_ReturnsEmptyList()
    {
        // Arrange
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>(),
            Edges = new List<DependencyEdge>()
        };

        // Act
        var cycles = CycleDetector.DetectCycles(graph);

        // Assert
        Assert.Empty(cycles);
    }

    [Fact]
    public void DetectCycles_MultipleCycles_DetectsAllCycles()
    {
        // Arrange
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode { Path = "A", Name = "A" },
                ["B"] = new ProjectNode { Path = "B", Name = "B" },
                ["C"] = new ProjectNode { Path = "C", Name = "C" },
                ["D"] = new ProjectNode { Path = "D", Name = "D" }
            },
            Edges = new List<DependencyEdge>
            {
                new() { FromProject = "A", ToProject = "B" },
                new() { FromProject = "B", ToProject = "A" },
                new() { FromProject = "C", ToProject = "D" },
                new() { FromProject = "D", ToProject = "C" }
            }
        };

        // Act
        var cycles = CycleDetector.DetectCycles(graph);

        // Assert
        Assert.True(cycles.Count >= 2);
    }
}

