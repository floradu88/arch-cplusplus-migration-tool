using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Utils;

/// <summary>
/// Calculates migration difficulty scores for projects.
/// Lower scores indicate easier migration to cross-platform.
/// </summary>
public static class MigrationScorer
{
    /// <summary>
    /// Calculates a migration difficulty score for a project (0-100, lower is easier).
    /// </summary>
    /// <param name="project">The project to score</param>
    /// <param name="graph">The dependency graph (for context)</param>
    /// <returns>Migration score from 0 (easy) to 100 (difficult)</returns>
    public static MigrationScore CalculateScore(ProjectNode project, DependencyGraph graph)
    {
        var score = new MigrationScore();
        int totalScore = 0;

        // Factor 1: Project Type (0-20 points)
        totalScore += ScoreProjectType(project, score);

        // Factor 2: Windows-specific dependencies (0-30 points)
        totalScore += ScoreWindowsDependencies(project, score);

        // Factor 3: Complexity - number of dependencies (0-15 points)
        totalScore += ScoreComplexity(project, graph, score);

        // Factor 4: External dependencies (0-20 points)
        totalScore += ScoreExternalDependencies(project, score);

        // Factor 5: Build system indicators (0-15 points)
        totalScore += ScoreBuildSystem(project, score);

        score.TotalScore = Math.Min(100, totalScore);
        score.DifficultyLevel = GetDifficultyLevel(score.TotalScore);

        return score;
    }

    private static int ScoreProjectType(ProjectNode project, MigrationScore score)
    {
        int points = 0;
        
        // Managed .NET projects are generally easier to migrate
        if (project.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            points = 5;
            score.Factors.Add("Managed .NET project", 5);
        }
        // Native C++ projects are harder
        else if (project.Path.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase))
        {
            points = 15;
            score.Factors.Add("Native C++ project", 15);
            
            // Executables are harder than libraries
            if (project.OutputType == "Exe")
            {
                points += 5;
                score.Factors.Add("Executable (may have UI dependencies)", 5);
            }
        }

        return points;
    }

    private static int ScoreWindowsDependencies(ProjectNode project, MigrationScore score)
    {
        int points = 0;
        var windowsKeywords = new[]
        {
            "win32", "winapi", "windows.h", "afx", "mfc", "atl", "com", "ole", "activex",
            "user32", "kernel32", "gdi32", "advapi32", "shell32", "ole32", "comdlg32",
            "ws2_32", "winsock", "directx", "d3d", "xaudio", "xinput"
        };

        // Check external dependencies for Windows-specific libraries
        foreach (var dep in project.ExternalDependencies)
        {
            var depLower = dep.ToLowerInvariant();
            foreach (var keyword in windowsKeywords)
            {
                if (depLower.Contains(keyword))
                {
                    points += 3;
                    score.Factors.Add($"Windows-specific dependency: {dep}", 3);
                    break; // Only count each dependency once
                }
            }
        }

        // Check project properties for Windows-specific settings
        if (project.Properties.TryGetValue("Platform", out var platform))
        {
            if (platform.Contains("Win32") || platform.Contains("x64") || platform.Contains("ARM64"))
            {
                // This is expected for Windows, but check for other indicators
            }
        }

        // Check for MFC/ATL usage (common in legacy projects)
        if (project.Properties.TryGetValue("UseOfMfc", out var useMfc))
        {
            if (useMfc == "true" || useMfc == "Static" || useMfc == "Dynamic")
            {
                points += 10;
                score.Factors.Add("Uses MFC (Microsoft Foundation Classes)", 10);
            }
        }

        if (project.Properties.TryGetValue("UseOfATL", out var useAtl))
        {
            if (useAtl == "true" || useAtl == "Static" || useAtl == "Dynamic")
            {
                points += 8;
                score.Factors.Add("Uses ATL (Active Template Library)", 8);
            }
        }

        return Math.Min(30, points); // Cap at 30 points
    }

    private static int ScoreComplexity(ProjectNode project, DependencyGraph graph, MigrationScore score)
    {
        int points = 0;

        // Count project dependencies
        var depCount = project.ProjectDependencies.Count;
        if (depCount > 10)
        {
            points += 10;
            score.Factors.Add($"High dependency count ({depCount} projects)", 10);
        }
        else if (depCount > 5)
        {
            points += 5;
            score.Factors.Add($"Moderate dependency count ({depCount} projects)", 5);
        }
        else if (depCount > 0)
        {
            score.Factors.Add($"Low dependency count ({depCount} projects)", 0);
        }

        // Check if project is part of a cycle (makes migration harder)
        var isInCycle = graph.Cycles.Any(cycle => cycle.Contains(project.Path));
        if (isInCycle)
        {
            points += 5;
            score.Factors.Add("Part of circular dependency", 5);
        }

        return Math.Min(15, points); // Cap at 15 points
    }

    private static int ScoreExternalDependencies(ProjectNode project, MigrationScore score)
    {
        int points = 0;
        var externalCount = project.ExternalDependencies.Count;

        if (externalCount > 20)
        {
            points += 15;
            score.Factors.Add($"Many external dependencies ({externalCount})", 15);
        }
        else if (externalCount > 10)
        {
            points += 10;
            score.Factors.Add($"Moderate external dependencies ({externalCount})", 10);
        }
        else if (externalCount > 5)
        {
            points += 5;
            score.Factors.Add($"Some external dependencies ({externalCount})", 5);
        }

        // Check for platform-specific external libraries
        var platformSpecificLibs = new[]
        {
            ".lib", ".dll", "nuget", "packages"
        };

        var hasPlatformSpecific = project.ExternalDependencies.Any(dep =>
            platformSpecificLibs.Any(lib => dep.Contains(lib, StringComparison.OrdinalIgnoreCase)));

        if (hasPlatformSpecific && externalCount > 0)
        {
            points += 5;
            score.Factors.Add("Platform-specific external libraries detected", 5);
        }

        return Math.Min(20, points); // Cap at 20 points
    }

    private static int ScoreBuildSystem(ProjectNode project, MigrationScore score)
    {
        int points = 0;

        // Legacy build systems are harder to migrate
        // Check for old Visual Studio project indicators
        if (project.Properties.ContainsKey("VisualStudioVersion"))
        {
            if (project.Properties.TryGetValue("VisualStudioVersion", out var vsVersion))
            {
                // Older VS versions indicate legacy code
                if (vsVersion.StartsWith("10.") || vsVersion.StartsWith("11.") || vsVersion.StartsWith("12."))
                {
                    points += 10;
                    score.Factors.Add($"Legacy Visual Studio version ({vsVersion})", 10);
                }
            }
        }

        // Check for custom build steps (indicates complexity)
        // This would require deeper project analysis, but we can infer from properties
        if (project.Properties.Count > 20)
        {
            points += 5;
            score.Factors.Add("Complex build configuration", 5);
        }

        return Math.Min(15, points); // Cap at 15 points
    }

    private static string GetDifficultyLevel(int score)
    {
        return score switch
        {
            < 20 => "Easy",
            < 40 => "Moderate",
            < 60 => "Hard",
            < 80 => "Very Hard",
            _ => "Extremely Hard"
        };
    }
}

/// <summary>
/// Represents a migration difficulty score for a project.
/// </summary>
public class MigrationScore
{
    public int TotalScore { get; set; }
    public string DifficultyLevel { get; set; } = string.Empty;
    public Dictionary<string, int> Factors { get; set; } = new();
}

