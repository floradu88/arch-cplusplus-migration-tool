namespace SolutionDependencyMapper.Models;

public sealed class ResolvedNuGetPackage
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? TargetFramework { get; set; }   // e.g., net8.0
    public bool IsDirect { get; set; }
}


