namespace SolutionDependencyMapper.Models;

public sealed class ProjectConfigurationSnapshot
{
    public string Configuration { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Key => string.IsNullOrWhiteSpace(Platform) ? Configuration : $"{Configuration}|{Platform}";

    // References captured after evaluating the project with (Configuration, Platform) global properties
    public List<string> ProjectDependencies { get; set; } = new();

    public List<NuGetPackageReference> NuGetPackageReferences { get; set; } = new();
    public List<string> FrameworkReferences { get; set; } = new();
    public List<string> AssemblyReferences { get; set; } = new();
    public List<string> ComReferences { get; set; } = new();
    public List<string> AnalyzerReferences { get; set; } = new();

    public List<string> NativeLibraries { get; set; } = new();
    public List<string> NativeDelayLoadDlls { get; set; } = new();
    public List<string> NativeLibraryDirectories { get; set; } = new();
    public List<string> IncludeDirectories { get; set; } = new();
    public List<string> HeaderFiles { get; set; } = new();

    public List<ReferenceValidationIssue> ReferenceValidationIssues { get; set; } = new();

    public OutputArtifactStatus? OutputArtifact { get; set; }
}


