using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Utils;
using Xunit;

namespace SolutionDependencyMapper.Tests.Utils;

public class MigrationScorerTests
{
    [Fact]
    public void CalculateScore_ManagedProject_LowScore()
    {
        // Arrange
        var project = new ProjectNode
        {
            Path = "Project.csproj",
            Name = "Project",
            OutputType = "DynamicLibrary",
            ProjectDependencies = new List<string>(),
            ExternalDependencies = new List<string>()
        };
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode> { [project.Path] = project },
            Edges = new List<DependencyEdge>(),
            Cycles = new List<List<string>>()
        };

        // Act
        var score = MigrationScorer.CalculateScore(project, graph);

        // Assert
        Assert.True(score.TotalScore < 20, "Managed projects should have low migration scores");
        Assert.Equal("Easy", score.DifficultyLevel);
        Assert.True(score.Factors.ContainsKey("Managed .NET project"));
    }

    [Fact]
    public void CalculateScore_NativeProjectWithMFC_HighScore()
    {
        // Arrange
        var project = new ProjectNode
        {
            Path = "Project.vcxproj",
            Name = "Project",
            OutputType = "Exe",
            ProjectDependencies = new List<string>(),
            ExternalDependencies = new List<string> { "mfc140.lib" },
            Properties = new Dictionary<string, string>
            {
                ["UseOfMfc"] = "true"
            }
        };
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode> { [project.Path] = project },
            Edges = new List<DependencyEdge>(),
            Cycles = new List<List<string>>()
        };

        // Act
        var score = MigrationScorer.CalculateScore(project, graph);

        // Assert
        Assert.True(score.TotalScore >= 30, "MFC projects should have high migration scores");
        Assert.Contains(score.Factors.Keys, k => k.Contains("MFC"));
    }

    [Fact]
    public void CalculateScore_ProjectWithWindowsDependencies_HigherScore()
    {
        // Arrange
        var project = new ProjectNode
        {
            Path = "Project.vcxproj",
            Name = "Project",
            OutputType = "DynamicLibrary",
            ProjectDependencies = new List<string>(),
            ExternalDependencies = new List<string> { "user32.lib", "kernel32.lib", "ws2_32.lib" }
        };
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode> { [project.Path] = project },
            Edges = new List<DependencyEdge>(),
            Cycles = new List<List<string>>()
        };

        // Act
        var score = MigrationScorer.CalculateScore(project, graph);

        // Assert
        Assert.True(score.TotalScore >= 15, "Projects with Windows dependencies should have higher scores");
        Assert.Contains(score.Factors.Keys, k => k.Contains("Windows-specific"));
    }

    [Fact]
    public void CalculateScore_ProjectInCycle_HigherScore()
    {
        // Arrange
        var project = new ProjectNode
        {
            Path = "Project.vcxproj",
            Name = "Project",
            OutputType = "DynamicLibrary",
            ProjectDependencies = new List<string> { "Other.vcxproj" }
        };
        var otherProject = new ProjectNode
        {
            Path = "Other.vcxproj",
            Name = "Other",
            OutputType = "StaticLibrary",
            ProjectDependencies = new List<string> { "Project.vcxproj" }
        };
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode> 
            { 
                [project.Path] = project,
                [otherProject.Path] = otherProject
            },
            Edges = new List<DependencyEdge>
            {
                new() { FromProject = "Project.vcxproj", ToProject = "Other.vcxproj" },
                new() { FromProject = "Other.vcxproj", ToProject = "Project.vcxproj" }
            },
            Cycles = new List<List<string>>
            {
                new() { "Project.vcxproj", "Other.vcxproj", "Project.vcxproj" }
            }
        };

        // Act
        var score = MigrationScorer.CalculateScore(project, graph);

        // Assert
        Assert.Contains(score.Factors.Keys, k => k.Contains("circular dependency"));
    }

    [Fact]
    public void CalculateScore_ProjectWithManyDependencies_HigherScore()
    {
        // Arrange
        var project = new ProjectNode
        {
            Path = "Project.vcxproj",
            Name = "Project",
            OutputType = "DynamicLibrary",
            ProjectDependencies = Enumerable.Range(1, 15).Select(i => $"Dep{i}.vcxproj").ToList(),
            ExternalDependencies = new List<string>()
        };
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode> { [project.Path] = project },
            Edges = new List<DependencyEdge>(),
            Cycles = new List<List<string>>()
        };

        // Act
        var score = MigrationScorer.CalculateScore(project, graph);

        // Assert
        Assert.Contains(score.Factors.Keys, k => k.Contains("High dependency count") || k.Contains("dependency count"));
    }

    [Fact]
    public void CalculateScore_ScoreNeverExceeds100()
    {
        // Arrange
        var project = new ProjectNode
        {
            Path = "Project.vcxproj",
            Name = "Project",
            OutputType = "Exe",
            ProjectDependencies = Enumerable.Range(1, 20).Select(i => $"Dep{i}.vcxproj").ToList(),
            ExternalDependencies = Enumerable.Range(1, 30).Select(i => $"lib{i}.lib").ToList(),
            Properties = new Dictionary<string, string>
            {
                ["UseOfMfc"] = "true",
                ["UseOfATL"] = "true"
            }
        };
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode> { [project.Path] = project },
            Edges = new List<DependencyEdge>(),
            Cycles = new List<List<string>>()
        };

        // Act
        var score = MigrationScorer.CalculateScore(project, graph);

        // Assert
        Assert.True(score.TotalScore <= 100, "Score should never exceed 100");
    }
}

