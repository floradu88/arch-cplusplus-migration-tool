using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Output;
using SolutionDependencyMapper.Utils;
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

        var projectPath = Path.Combine(tempDir, "ProjectA.vcxproj");
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                [projectPath] = new ProjectNode
                {
                    Path = projectPath,
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { projectPath } }
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

        var projectPath = Path.Combine(tempDir, "ProjectA.vcxproj");
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                [projectPath] = new ProjectNode
                {
                    Path = projectPath,
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { projectPath } }
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

        var projectPath = Path.Combine(tempDir, "ProjectA.vcxproj");
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                [projectPath] = new ProjectNode
                {
                    Path = projectPath,
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { projectPath } }
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

        var projectPath = Path.Combine(tempDir, "ProjectA.vcxproj");
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                [projectPath] = new ProjectNode
                {
                    Path = projectPath,
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { projectPath } }
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

    [Fact]
    public void GenerateAll_WithToolsContext_UsesDiscoveredTools()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var solutionPath = Path.Combine(tempDir, "Test.sln");

        var projectPath = Path.Combine(tempDir, "ProjectA.vcxproj");
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                [projectPath] = new ProjectNode
                {
                    Path = projectPath,
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { projectPath } }
            },
            Cycles = new List<List<string>>()
        };

        var toolsContext = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["msbuild.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Test\MSBuild.exe",
                        Source = ToolFinder.ToolSource.Vswhere
                    }
                },
                ["cmake.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "cmake.exe",
                        Path = @"C:\Test\CMake.exe",
                        Source = ToolFinder.ToolSource.EnvironmentPath
                    }
                }
            }
        };

        try
        {
            // Act
            BuildScriptGenerator.GenerateAll(graph, solutionPath, tempDir, "Release", "x64", toolsContext);

            // Assert
            var ps1Content = File.ReadAllText(Path.Combine(tempDir, "build.ps1"));
            Assert.Contains("discovered MSBuild locations", ps1Content);
            Assert.Contains(@"C:\Test\MSBuild.exe", ps1Content);

            var batContent = File.ReadAllText(Path.Combine(tempDir, "build.bat"));
            Assert.Contains(@"C:\Test\MSBuild.exe", batContent);

            var shContent = File.ReadAllText(Path.Combine(tempDir, "build.sh"));
            Assert.Contains("discovered CMake", shContent);
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
    public void GenerateAll_WithToolsContext_PowerShellScript_ContainsDiscoveredPaths()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var solutionPath = Path.Combine(tempDir, "Test.sln");

        var projectPath = Path.Combine(tempDir, "ProjectA.vcxproj");
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                [projectPath] = new ProjectNode
                {
                    Path = projectPath,
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { projectPath } }
            },
            Cycles = new List<List<string>>()
        };

        var toolsContext = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["msbuild.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\VS2025\MSBuild.exe",
                        Source = ToolFinder.ToolSource.CommonLocation
                    },
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\VS2026\MSBuild.exe",
                        Source = ToolFinder.ToolSource.Vswhere
                    }
                }
            }
        };

        try
        {
            // Act
            BuildScriptGenerator.GenerateAll(graph, solutionPath, tempDir, "Release", "x64", toolsContext);

            // Assert
            var ps1Content = File.ReadAllText(Path.Combine(tempDir, "build.ps1"));
            Assert.Contains("discovered MSBuild locations", ps1Content);
            Assert.Contains(@"C:\VS2026\MSBuild.exe", ps1Content); // Should prefer vswhere
            Assert.Contains(@"C:\VS2025\MSBuild.exe", ps1Content);
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
    public void GenerateAll_WithNullToolsContext_FallsBackToDefault()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var solutionPath = Path.Combine(tempDir, "Test.sln");

        var projectPath = Path.Combine(tempDir, "ProjectA.vcxproj");
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                [projectPath] = new ProjectNode
                {
                    Path = projectPath,
                    Name = "ProjectA",
                    OutputType = "StaticLibrary"
                }
            },
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { projectPath } }
            },
            Cycles = new List<List<string>>()
        };

        try
        {
            // Act
            BuildScriptGenerator.GenerateAll(graph, solutionPath, tempDir, "Release", "x64", null);

            // Assert - Should still generate scripts with fallback logic
            Assert.True(File.Exists(Path.Combine(tempDir, "build.ps1")));
            Assert.True(File.Exists(Path.Combine(tempDir, "build.bat")));
            
            var ps1Content = File.ReadAllText(Path.Combine(tempDir, "build.ps1"));
            Assert.Contains("vswhere", ps1Content); // Should have fallback logic
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

