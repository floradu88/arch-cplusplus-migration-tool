namespace SolutionDependencyMapper.Models;

/// <summary>
/// Represents a single project in the solution with its metadata and dependencies.
/// </summary>
public class ProjectNode
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string OutputType { get; set; } = string.Empty;
    public string OutputBinary { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string TargetExtension { get; set; } = string.Empty;
    public List<string> ProjectDependencies { get; set; } = new();

    /// <summary>
    /// Backward-compatible "flat" dependency list (used by existing outputs).
    /// Prefer the structured reference fields below for new functionality.
    /// </summary>
    public List<string> ExternalDependencies { get; set; } = new();
    public Dictionary<string, string> Properties { get; set; } = new();

    // -------------------- Structured references (new) --------------------

    // Managed (.csproj/.vbproj/.fsproj) references
    public List<NuGetPackageReference> NuGetPackageReferences { get; set; } = new();
    public List<string> FrameworkReferences { get; set; } = new();
    public List<string> AssemblyReferences { get; set; } = new();
    public List<string> ComReferences { get; set; } = new();
    public List<string> AnalyzerReferences { get; set; } = new();

    // Native (vcxproj/vcproj) references
    public List<string> NativeLibraries { get; set; } = new();              // e.g., user32.lib
    public List<string> NativeDelayLoadDlls { get; set; } = new();          // e.g., foo.dll
    public List<string> NativeLibraryDirectories { get; set; } = new();     // e.g., $(ProjectDir)lib;C:\3rdparty\lib
    public List<string> IncludeDirectories { get; set; } = new();           // e.g., $(ProjectDir)include;C:\3rdparty\include
    public List<string> HeaderFiles { get; set; } = new();                  // e.g., *.h tracked as project items
    
    // .NET Target Framework (for .csproj files)
    public string? TargetFramework { get; set; }
    
    // Project type (e.g., "C# Project", "C++ Project", "VB Project")
    public string? ProjectType { get; set; }
    
    // MSBuild ToolsVersion (e.g., "15.0", "16.0", "Current")
    public string? ToolsVersion { get; set; }
    
    // Migration scoring (optional, calculated by MigrationScorer)
    public int? MigrationScore { get; set; }
    public string? MigrationDifficultyLevel { get; set; }
}

