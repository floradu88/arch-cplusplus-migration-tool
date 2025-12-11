using SolutionDependencyMapper.Utils;
using Xunit;

namespace SolutionDependencyMapper.Tests.Utils;

public class ToolFinderTests
{
    [Fact]
    public void FindTool_WithNonExistentTool_ReturnsEmptyList()
    {
        // Arrange
        var toolName = "nonexistent-tool-that-does-not-exist.exe";

        // Act
        var result = ToolFinder.FindTool(toolName);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void FindAllTools_ReturnsDictionary()
    {
        // Act
        var result = ToolFinder.FindAllTools();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, List<ToolFinder.FoundTool>>>(result);
    }

    [Fact]
    public void FindTool_WithProjectRoot_SearchesInDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testTool = Path.Combine(tempDir, "test-tool.exe");
        File.WriteAllText(testTool, "fake tool");

        try
        {
            // Act
            var result = ToolFinder.FindTool("test-tool.exe", tempDir);

            // Assert
            Assert.NotEmpty(result);
            Assert.Contains(result, t => t.Path.Equals(testTool, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, t => t.Source == ToolFinder.ToolSource.ProjectRoot);
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
    public void GetToolVersion_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");

        // Act
        var result = ToolFinder.GetToolVersion(nonExistentPath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetToolVersion_WithExistingExecutable_MayReturnVersion()
    {
        // Arrange - Use a system executable that should exist
        var systemExe = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe")
            : "/bin/sh";

        if (File.Exists(systemExe))
        {
            // Act
            var result = ToolFinder.GetToolVersion(systemExe);

            // Assert - Version may or may not be available, but should not throw
            // If version is available, it should be a non-empty string
            if (result != null)
            {
                Assert.NotEmpty(result);
            }
        }
    }

    [Fact]
    public void PrintFoundTools_WithEmptyDictionary_PrintsNoToolsMessage()
    {
        // Arrange
        var tools = new Dictionary<string, List<ToolFinder.FoundTool>>();
        var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            ToolFinder.PrintFoundTools(tools);

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("No tools found", output);
        }
        finally
        {
            var standardOutput = new StreamWriter(Console.OpenStandardOutput());
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);
        }
    }

    [Fact]
    public void PrintFoundTools_WithTools_PrintsFormattedOutput()
    {
        // Arrange
        var tools = new Dictionary<string, List<ToolFinder.FoundTool>>
        {
            ["msbuild.exe"] = new List<ToolFinder.FoundTool>
            {
                new ToolFinder.FoundTool
                {
                    Name = "msbuild.exe",
                    Path = @"C:\Test\MSBuild.exe",
                    Source = ToolFinder.ToolSource.CommonLocation,
                    Version = "17.0.0"
                }
            }
        };
        var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            ToolFinder.PrintFoundTools(tools);

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("msbuild.exe", output);
            Assert.Contains("C:\\Test\\MSBuild.exe", output);
        }
        finally
        {
            var standardOutput = new StreamWriter(Console.OpenStandardOutput());
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);
        }
    }

    [Fact]
    public void FoundTool_Properties_AreSetCorrectly()
    {
        // Arrange & Act
        var tool = new ToolFinder.FoundTool
        {
            Name = "test.exe",
            Path = @"C:\Test\test.exe",
            Source = ToolFinder.ToolSource.EnvironmentPath,
            Version = "1.0.0"
        };

        // Assert
        Assert.Equal("test.exe", tool.Name);
        Assert.Equal(@"C:\Test\test.exe", tool.Path);
        Assert.Equal(ToolFinder.ToolSource.EnvironmentPath, tool.Source);
        Assert.Equal("1.0.0", tool.Version);
    }
}

