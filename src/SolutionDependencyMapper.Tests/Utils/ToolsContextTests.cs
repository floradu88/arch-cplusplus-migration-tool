using SolutionDependencyMapper.Utils;
using Xunit;

namespace SolutionDependencyMapper.Tests.Utils;

public class ToolsContextTests
{
    [Fact]
    public void GetMSBuildPath_WithNoTools_ReturnsNull()
    {
        // Arrange
        var context = new ToolsContext();

        // Act
        var result = context.GetMSBuildPath();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetMSBuildPath_WithMultipleTools_PrefersVswhere()
    {
        // Arrange
        var context = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["msbuild.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Common\MSBuild.exe",
                        Source = ToolFinder.ToolSource.CommonLocation
                    },
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Path\MSBuild.exe",
                        Source = ToolFinder.ToolSource.EnvironmentPath
                    },
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Vswhere\MSBuild.exe",
                        Source = ToolFinder.ToolSource.Vswhere
                    }
                }
            }
        };

        // Act
        var result = context.GetMSBuildPath();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(@"C:\Vswhere\MSBuild.exe", result);
    }

    [Fact]
    public void GetMSBuildPath_WithNoVswhere_PrefersEnvironmentPath()
    {
        // Arrange
        var context = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["msbuild.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Common\MSBuild.exe",
                        Source = ToolFinder.ToolSource.CommonLocation
                    },
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Path\MSBuild.exe",
                        Source = ToolFinder.ToolSource.EnvironmentPath
                    }
                }
            }
        };

        // Act
        var result = context.GetMSBuildPath();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(@"C:\Path\MSBuild.exe", result);
    }

    [Fact]
    public void GetCmakePath_WithNoTools_ReturnsNull()
    {
        // Arrange
        var context = new ToolsContext();

        // Act
        var result = context.GetCmakePath();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCmakePath_WithTools_PrefersEnvironmentPath()
    {
        // Arrange
        var context = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["cmake.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "cmake.exe",
                        Path = @"C:\Common\CMake.exe",
                        Source = ToolFinder.ToolSource.CommonLocation
                    },
                    new ToolFinder.FoundTool
                    {
                        Name = "cmake.exe",
                        Path = @"C:\Path\CMake.exe",
                        Source = ToolFinder.ToolSource.EnvironmentPath
                    }
                }
            }
        };

        // Act
        var result = context.GetCmakePath();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(@"C:\Path\CMake.exe", result);
    }

    [Fact]
    public void GetToolPath_WithExistingTool_ReturnsPath()
    {
        // Arrange
        var context = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["cl.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "cl.exe",
                        Path = @"C:\VC\cl.exe",
                        Source = ToolFinder.ToolSource.CommonLocation
                    }
                }
            }
        };

        // Act
        var result = context.GetToolPath("cl.exe");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(@"C:\VC\cl.exe", result);
    }

    [Fact]
    public void GetToolPath_WithNonExistentTool_ReturnsNull()
    {
        // Arrange
        var context = new ToolsContext();

        // Act
        var result = context.GetToolPath("nonexistent.exe");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetToolPaths_WithMultipleInstances_ReturnsAllPaths()
    {
        // Arrange
        var context = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["msbuild.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Path1\MSBuild.exe",
                        Source = ToolFinder.ToolSource.EnvironmentPath
                    },
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Path2\MSBuild.exe",
                        Source = ToolFinder.ToolSource.CommonLocation
                    }
                }
            }
        };

        // Act
        var result = context.GetToolPaths("msbuild.exe");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(@"C:\Path1\MSBuild.exe", result);
        Assert.Contains(@"C:\Path2\MSBuild.exe", result);
    }

    [Fact]
    public void GetToolPaths_WithNonExistentTool_ReturnsEmptyList()
    {
        // Arrange
        var context = new ToolsContext();

        // Act
        var result = context.GetToolPaths("nonexistent.exe");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void HasTool_WithExistingTool_ReturnsTrue()
    {
        // Arrange
        var context = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["msbuild.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\MSBuild.exe",
                        Source = ToolFinder.ToolSource.CommonLocation
                    }
                }
            }
        };

        // Act
        var result = context.HasTool("msbuild.exe");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasTool_WithNonExistentTool_ReturnsFalse()
    {
        // Arrange
        var context = new ToolsContext();

        // Act
        var result = context.HasTool("nonexistent.exe");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasTool_WithEmptyToolList_ReturnsFalse()
    {
        // Arrange
        var context = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["msbuild.exe"] = new List<ToolFinder.FoundTool>()
            }
        };

        // Act
        var result = context.HasTool("msbuild.exe");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetMSBuildPathsForScript_ReturnsOrderedPaths()
    {
        // Arrange
        var context = new ToolsContext
        {
            AllTools = new Dictionary<string, List<ToolFinder.FoundTool>>
            {
                ["msbuild.exe"] = new List<ToolFinder.FoundTool>
                {
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Common\MSBuild.exe",
                        Source = ToolFinder.ToolSource.CommonLocation
                    },
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Path\MSBuild.exe",
                        Source = ToolFinder.ToolSource.EnvironmentPath
                    },
                    new ToolFinder.FoundTool
                    {
                        Name = "msbuild.exe",
                        Path = @"C:\Vswhere\MSBuild.exe",
                        Source = ToolFinder.ToolSource.Vswhere
                    }
                }
            }
        };

        // Act
        var result = context.GetMSBuildPathsForScript();

        // Assert
        Assert.Equal(3, result.Count);
        // Should be ordered by preference: vswhere, environment, common
        Assert.Equal(@"C:\Vswhere\MSBuild.exe", result[0]);
        Assert.Equal(@"C:\Path\MSBuild.exe", result[1]);
        Assert.Equal(@"C:\Common\MSBuild.exe", result[2]);
    }
}

