using System.Text.RegularExpressions;

namespace SolutionDependencyMapper.Core;

/// <summary>
/// Loads and parses Visual Studio solution (.sln) files to extract project paths.
/// </summary>
public class SolutionLoader
{
    /// <summary>
    /// Extracts all project file paths from a solution file.
    /// </summary>
    /// <param name="solutionPath">Path to the .sln file</param>
    /// <returns>List of absolute paths to project files (.vcxproj, .csproj)</returns>
    public static List<string> ExtractProjectsFromSolution(string solutionPath)
    {
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");
        }

        var projectPaths = new List<string>();
        var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;

        // Pattern to match Project lines in .sln file
        // Format: Project("{GUID}") = "Name", "Path", "{GUID}"
        var projectPattern = new Regex(
            @"^Project\(""\{[A-F0-9-]+\}""\)\s*=\s*""[^""]+"",\s*""([^""]+)"",\s*""\{[A-F0-9-]+\}""",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        );

        var solutionContent = File.ReadAllText(solutionPath);
        var matches = projectPattern.Matches(solutionContent);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var relativePath = match.Groups[1].Value.Trim();
                
                // Only include actual project files
                if (relativePath.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) ||
                    relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    // Resolve relative path to absolute path
                    var fullPath = Path.GetFullPath(Path.Combine(solutionDirectory, relativePath));
                    
                    if (File.Exists(fullPath))
                    {
                        projectPaths.Add(fullPath);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Project file not found: {fullPath}");
                    }
                }
            }
        }

        return projectPaths;
    }
}

