using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Output;
using Xunit;

namespace SolutionDependencyMapper.Tests.Output;

public class BuildScriptGeneratorTests
{
    [Fact]
    public void GenerateAll_CreatesAllScriptFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var solutionPath = Path.Combine(tempDir, "Test.sln");

        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode
                {
                    Path = Path.Combine(tempDir, "ProjectA.vcxproj"),
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { Path.Combine(tempDir, "ProjectA.vcxproj") } }
            },
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            BuildScriptGenerator.GenerateAll(graph, solutionPath, tempDir);

            // Assert
            Assert.True(File.Exists(Path.Combine(tempDir, "build-layers.json")));
            Assert.True(File.Exists(Path.Combine(tempDir, "build.ps1")));
            Assert.True(File.Exists(Path.Combine(tempDir, "build.bat")));
            Assert.True(File.Exists(Path.Combine(tempDir, "build.sh")));
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
    public void GenerateAll_PowerShellScript_ContainsMSBuildDetection()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var solutionPath = Path.Combine(tempDir, "Test.sln");

        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode
                {
                    Path = Path.Combine(tempDir, "ProjectA.vcxproj"),
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { Path.Combine(tempDir, "ProjectA.vcxproj") } }
            },
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            BuildScriptGenerator.GenerateAll(graph, solutionPath, tempDir);

            // Assert
            var ps1Content = File.ReadAllText(Path.Combine(tempDir, "build.ps1"));
            Assert.Contains("Locate MSBuild", ps1Content);
            Assert.Contains("vswhere", ps1Content);
            Assert.Contains("ProjectA", ps1Content);
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
    public void GenerateAll_BatchScript_ContainsMSBuildDetection()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var solutionPath = Path.Combine(tempDir, "Test.sln");

        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode
                {
                    Path = Path.Combine(tempDir, "ProjectA.vcxproj"),
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { Path.Combine(tempDir, "ProjectA.vcxproj") } }
            },
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            BuildScriptGenerator.GenerateAll(graph, solutionPath, tempDir);

            // Assert
            var batContent = File.ReadAllText(Path.Combine(tempDir, "build.bat"));
            Assert.Contains("Locate MSBuild", batContent);
            Assert.Contains("vswhere", batContent);
            Assert.Contains("ProjectA", batContent);
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
    public void GenerateAll_ShellScript_ContainsCMakeCommands()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var solutionPath = Path.Combine(tempDir, "Test.sln");

        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A"] = new ProjectNode
                {
                    Path = Path.Combine(tempDir, "ProjectA.vcxproj"),
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { Path.Combine(tempDir, "ProjectA.vcxproj") } }
            },
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            BuildScriptGenerator.GenerateAll(graph, solutionPath, tempDir);

            // Assert
            var shContent = File.ReadAllText(Path.Combine(tempDir, "build.sh"));
            Assert.Contains("#!/bin/bash", shContent);
            Assert.Contains("cmake", shContent);
            Assert.Contains("ProjectA", shContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}

