namespace SolutionDependencyMapper.Models;

public sealed class SolutionConfigurationMapping
{
    // Solution config/platform (what VS solution is building)
    public ConfigurationPlatform Solution { get; set; } = new();

    // Project config/platform used when solution config/platform is active
    public ConfigurationPlatform Project { get; set; } = new();

    public bool Build { get; set; }
    public bool Deploy { get; set; }
}


