namespace SolutionDependencyMapper.Models;

public sealed class SolutionInfo
{
    public string SolutionPath { get; set; } = string.Empty;
    public List<SolutionProjectInfo> Projects { get; set; } = new();
}

public sealed class SolutionProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string ProjectGuid { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;

    // solution config/platform -> mapping
    public List<SolutionConfigurationMapping> ConfigurationMappings { get; set; } = new();
}


