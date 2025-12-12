using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Output;
using Xunit;

namespace SolutionDependencyMapper.Tests.Output;

public class LayeredSolutionGeneratorTests
{
    [Fact]
    public void Generate_AddsLayerFoldersNestedProjectsAndDependencies()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var slnPath = Path.Combine(tempDir, "Test.sln");
        var outPath = Path.Combine(tempDir, "layered-build.sln");

        var aGuid = "{11111111-1111-1111-1111-111111111111}";
        var bGuid = "{22222222-2222-2222-2222-222222222222}";
        var cGuid = "{33333333-3333-3333-3333-333333333333}";

        File.WriteAllText(slnPath, $@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Project(""{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}"") = ""A"", ""A\A.csproj"", ""{aGuid}""
EndProject
Project(""{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}"") = ""B"", ""B\B.csproj"", ""{bGuid}""
EndProject
Project(""{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}"") = ""C"", ""C\C.csproj"", ""{cGuid}""
EndProject
Global
\tGlobalSection(SolutionConfigurationPlatforms) = preSolution
\t\tDebug|Any CPU = Debug|Any CPU
\tEndGlobalSection
EndGlobal
".Trim());

        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>
            {
                ["A\\A.csproj"] = new ProjectNode { Path = "A\\A.csproj", SolutionProjectGuid = aGuid },
                ["B\\B.csproj"] = new ProjectNode { Path = "B\\B.csproj", SolutionProjectGuid = bGuid },
                ["C\\C.csproj"] = new ProjectNode { Path = "C\\C.csproj", SolutionProjectGuid = cGuid }
            },
            BuildLayers = new List<BuildLayer>
            {
                new() { LayerNumber = 0, ProjectPaths = new List<string> { "A\\A.csproj", "B\\B.csproj" } },
                new() { LayerNumber = 1, ProjectPaths = new List<string> { "C\\C.csproj" } }
            }
        };

        try
        {
            LayeredSolutionGenerator.Generate(slnPath, graph, outPath);
            var text = File.ReadAllText(outPath);

            Assert.Contains("Layer 00", text);
            Assert.Contains("Layer 01", text);

            // NestedProjects section should exist and map projects to folders
            Assert.Contains("GlobalSection(NestedProjects)", text);
            Assert.Contains(aGuid.ToUpperInvariant(), text);
            Assert.Contains(bGuid.ToUpperInvariant(), text);
            Assert.Contains(cGuid.ToUpperInvariant(), text);

            // C should depend on A and B (previous layer) via ProjectDependencies
            Assert.Contains("ProjectSection(ProjectDependencies)", text);
            Assert.Contains($"{aGuid.ToUpperInvariant()} = {aGuid.ToUpperInvariant()}", text);
            Assert.Contains($"{bGuid.ToUpperInvariant()} = {bGuid.ToUpperInvariant()}", text);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}


