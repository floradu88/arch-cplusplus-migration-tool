using SolutionDependencyMapper.Core;
using Xunit;

namespace SolutionDependencyMapper.Tests.Core;

public class SolutionLoaderTests
{
    [Fact]
    public void ExtractProjectsFromSolution_ValidSolution_ReturnsProjectPaths()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var solutionPath = Path.Combine(tempDir, "TestSolution.sln");
        var project1Path = Path.Combine(tempDir, "Project1.vcxproj");
        var project2Path = Path.Combine(tempDir, "Project2.csproj");
        var project3Path = Path.Combine(tempDir, "Project3.vbproj");
        var project4Path = Path.Combine(tempDir, "Project4.vcproj");

        // Create test solution file
        var solutionContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project1"", ""Project1.vcxproj"", ""{11111111-1111-1111-1111-111111111111}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project2"", ""Project2.csproj"", ""{22222222-2222-2222-2222-222222222222}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project3"", ""Project3.vbproj"", ""{33333333-3333-3333-3333-333333333333}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project4"", ""Project4.vcproj"", ""{44444444-4444-4444-4444-444444444444}""
EndProject
";
        File.WriteAllText(solutionPath, solutionContent);
        File.WriteAllText(project1Path, "<Project></Project>");
        File.WriteAllText(project2Path, "<Project></Project>");
        File.WriteAllText(project3Path, "<Project></Project>");
        File.WriteAllText(project4Path, "<Project></Project>");

        try
        {
            // Act
            var result = SolutionLoader.ExtractProjectsFromSolution(solutionPath);

            // Assert
            Assert.Equal(4, result.Count);
            Assert.Contains(project1Path, result);
            Assert.Contains(project2Path, result);
            Assert.Contains(project3Path, result);
            Assert.Contains(project4Path, result);
        }
        finally
        {
            // Cleanup
            if (File.Exists(solutionPath)) File.Delete(solutionPath);
            if (File.Exists(project1Path)) File.Delete(project1Path);
            if (File.Exists(project2Path)) File.Delete(project2Path);
            if (File.Exists(project3Path)) File.Delete(project3Path);
            if (File.Exists(project4Path)) File.Delete(project4Path);
        }
    }

    [Fact]
    public void ExtractProjectsFromSolution_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent.sln");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            SolutionLoader.ExtractProjectsFromSolution(nonExistentPath));
    }

    [Fact]
    public void ExtractProjectsFromSolution_EmptySolution_ReturnsEmptyList()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var solutionPath = Path.Combine(tempDir, "EmptySolution.sln");
        var solutionContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
";
        File.WriteAllText(solutionPath, solutionContent);

        try
        {
            // Act
            var result = SolutionLoader.ExtractProjectsFromSolution(solutionPath);

            // Assert
            Assert.Empty(result);
        }
        finally
        {
            if (File.Exists(solutionPath)) File.Delete(solutionPath);
        }
    }

    [Fact]
    public void ExtractProjectsFromSolution_IgnoresNonProjectFiles()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var solutionPath = Path.Combine(tempDir, "TestSolution.sln");
        var solutionContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""Solution Items"", ""Solution Items"", ""{33333333-3333-3333-3333-333333333333}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project1"", ""Project1.vcxproj"", ""{11111111-1111-1111-1111-111111111111}""
EndProject
";
        File.WriteAllText(solutionPath, solutionContent);
        var project1Path = Path.Combine(tempDir, "Project1.vcxproj");
        File.WriteAllText(project1Path, "<Project></Project>");

        try
        {
            // Act
            var result = SolutionLoader.ExtractProjectsFromSolution(solutionPath);

            // Assert
            Assert.Single(result);
            Assert.Contains(project1Path, result);
        }
        finally
        {
            if (File.Exists(solutionPath)) File.Delete(solutionPath);
            if (File.Exists(project1Path)) File.Delete(project1Path);
        }
    }
}

