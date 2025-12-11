namespace SolutionDependencyMapper.Models;

public sealed class NuGetPackageReference
{
    public string Id { get; set; } = string.Empty;
    public string? Version { get; set; }

    // Common MSBuild metadata fields (optional)
    public string? PrivateAssets { get; set; }
    public string? IncludeAssets { get; set; }
    public string? ExcludeAssets { get; set; }
}


