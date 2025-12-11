namespace SolutionDependencyMapper.Utils;

/// <summary>
/// Utility class for detecting and installing missing Microsoft.Build packages in project files.
/// Handles automatic package installation and NuGet restore for projects that fail due to missing dependencies.
/// </summary>
public static class PackageInstaller
{
    private const string DefaultMsBuildPackageVersion = "15.1.548";

    // Common Microsoft.Build packages that projects might need
    private static readonly Dictionary<string, string> CommonMsBuildPackages = new()
    {
        { "Microsoft.Build", DefaultMsBuildPackageVersion },
        { "Microsoft.Build.Framework", DefaultMsBuildPackageVersion },
        { "Microsoft.Build.Utilities.Core", DefaultMsBuildPackageVersion },
        { "Microsoft.Build.Tasks.Core", DefaultMsBuildPackageVersion },
        { "Microsoft.Build.Engine", DefaultMsBuildPackageVersion }
    };

    /// <summary>
    /// Detects if an exception is related to missing Microsoft.Build packages.
    /// </summary>
    public static bool IsMissingPackageError(Exception ex)
    {
        return MsBuildPackageInference.IsMatch(ex);
    }

    /// <summary>
    /// Returns the list of Microsoft.Build-related NuGet packages to install based on the error contents.
    /// This is intentionally conservative: it only triggers on clear Microsoft.Build* assembly/type load failures.
    /// </summary>
    public static IReadOnlyCollection<string> GetMissingPackagesFromError(Exception ex)
    {
        return MsBuildPackageInference.InferPackages(ex);
    }

    /// <summary>
    /// Attempts to fix a project by installing missing packages inferred from the exception and restoring.
    /// </summary>
    public static bool FixProjectPackagesForError(string projectPath, Exception ex)
    {
        if (!File.Exists(projectPath))
            return false;

        var packagesToInstall = GetMissingPackagesFromError(ex);
        if (packagesToInstall.Count == 0)
            return false;

        return FixProjectPackages(projectPath, packagesToInstall);
    }

    /// <summary>
    /// Installs missing Microsoft.Build packages to a .csproj file.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file</param>
    /// <param name="packagesToInstall">Optional list of specific packages to install. If null, installs common packages.</param>
    /// <returns>True if packages were installed, false if the file couldn't be modified or isn't a .csproj</returns>
    public static bool InstallMsBuildPackages(string projectPath, IEnumerable<string>? packagesToInstall = null)
    {
        if (!File.Exists(projectPath))
            return false;

        // Only process .csproj files
        if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var packages = packagesToInstall?.ToList() ?? CommonMsBuildPackages.Keys.ToList();
            var editor = new CsprojPackageReferenceEditor(projectPath);
            var added = editor.AddPackageReferences(packages.Select(p => (Name: p, Version: GetVersionOrDefault(p))));
            if (added)
            {
                Console.WriteLine($"  ✓ Installed Microsoft.Build packages to: {Path.GetFileName(projectPath)}");
            }
            return added;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Warning: Could not install packages to {Path.GetFileName(projectPath)}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores NuGet packages for a project using dotnet restore.
    /// </summary>
    /// <param name="projectPath">Path to the project file</param>
    /// <returns>True if restore succeeded, false otherwise</returns>
    public static bool RestorePackages(string projectPath)
    {
        return DotnetCli.Restore(projectPath);
    }

    /// <summary>
    /// Attempts to fix a project by installing missing packages and restoring.
    /// </summary>
    /// <param name="projectPath">Path to the project file</param>
    /// <returns>True if fixes were applied, false otherwise</returns>
    public static bool FixProjectPackages(string projectPath)
    {
        return FixProjectPackages(projectPath, packagesToInstall: null);
    }

    private static bool FixProjectPackages(string projectPath, IReadOnlyCollection<string>? packagesToInstall)
    {
        if (!File.Exists(projectPath))
            return false;

        var changed = InstallMsBuildPackages(projectPath, packagesToInstall);
        var restored = RestorePackages(projectPath);

        return changed || restored;
    }

    private static string GetVersionOrDefault(string packageName)
    {
        return CommonMsBuildPackages.TryGetValue(packageName, out var v) ? v : DefaultMsBuildPackageVersion;
    }
}

