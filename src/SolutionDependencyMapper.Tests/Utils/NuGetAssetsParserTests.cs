using SolutionDependencyMapper.Utils;
using Xunit;

namespace SolutionDependencyMapper.Tests.Utils;

public class NuGetAssetsParserTests
{
    [Fact]
    public void TryParseResolvedPackages_ReturnsEmpty_WhenFileMissing()
    {
        var result = NuGetAssetsParser.TryParseResolvedPackages(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.Empty(result);
    }

    [Fact]
    public void TryParseResolvedPackages_ParsesDirectAndTransitive_PerTfm()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, "project.assets.json");

        var json = @"{
  ""version"": 3,
  ""project"": {
    ""frameworks"": {
      ""net8.0"": {
        ""dependencies"": {
          ""Newtonsoft.Json"": ""[13.0.3, )""
        }
      }
    }
  },
  ""targets"": {
    ""net8.0"": {
      ""Newtonsoft.Json/13.0.3"": { ""type"": ""package"" },
      ""System.Memory/4.5.5"": { ""type"": ""package"" }
    }
  }
}";

        File.WriteAllText(path, json);

        try
        {
            var pkgs = NuGetAssetsParser.TryParseResolvedPackages(path);
            Assert.Contains(pkgs, p => p.TargetFramework == "net8.0" && p.Id == "Newtonsoft.Json" && p.Version == "13.0.3" && p.IsDirect);
            Assert.Contains(pkgs, p => p.TargetFramework == "net8.0" && p.Id == "System.Memory" && p.Version == "4.5.5" && !p.IsDirect);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}


