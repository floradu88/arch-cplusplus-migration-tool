using System.Text.RegularExpressions;
using SolutionDependencyMapper.Models;

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
        return ExtractSolutionInfo(solutionPath).Projects.Select(p => p.FullPath).ToList();
    }

    /// <summary>
    /// Extracts solution projects and solution-level configuration/platform mappings.
    /// </summary>
    public static SolutionInfo ExtractSolutionInfo(string solutionPath)
    {
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
        var content = File.ReadAllText(solutionPath);

        var info = new SolutionInfo
        {
            SolutionPath = solutionPath
        };

        // Project lines in .sln:
        // Project("{TYPE_GUID}") = "Name", "Path", "{PROJECT_GUID}"
        var projectPattern = new Regex(
            @"^Project\(""\{(?<typeGuid>[A-F0-9-]+)\}""\)\s*=\s*""(?<name>[^""]+)"",\s*""(?<path>[^""]+)"",\s*""\{(?<guid>[A-F0-9-]+)\}""",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        );

        var projectsByGuid = new Dictionary<string, SolutionProjectInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in projectPattern.Matches(content))
        {
            var name = m.Groups["name"].Value.Trim();
            var relativePath = m.Groups["path"].Value.Trim();
            var projectGuid = m.Groups["guid"].Value.Trim();

            if (!IsSupportedProjectPath(relativePath))
                continue;

            var fullPath = Path.GetFullPath(Path.Combine(solutionDirectory, relativePath));
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"Warning: Project file not found: {fullPath}");
                continue;
            }

            var p = new SolutionProjectInfo
            {
                Name = name,
                ProjectGuid = projectGuid,
                RelativePath = relativePath,
                FullPath = fullPath
            };

            projectsByGuid[projectGuid] = p;
        }

        // GlobalSection(ProjectConfigurationPlatforms) parsing
        // Example line:
        //   {GUID}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        //   {GUID}.Debug|Any CPU.Build.0 = Debug|Any CPU
        var sectionMatch = Regex.Match(
            content,
            @"GlobalSection\(ProjectConfigurationPlatforms\)\s*=\s*postSolution(?<body>[\s\S]*?)EndGlobalSection",
            RegexOptions.IgnoreCase
        );

        if (sectionMatch.Success)
        {
            var body = sectionMatch.Groups["body"].Value;
            var linePattern = new Regex(
                @"^\s*\{(?<guid>[A-F0-9-]+)\}\.(?<sol>[^.]+)\.(?<key>ActiveCfg|Build\.0|Deploy\.0)\s*=\s*(?<val>.+?)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline
            );

            // Build intermediate structure: guid -> solKey -> mapping state
            var state = new Dictionary<(string Guid, string SolKey), (string? ActiveCfg, bool Build, bool Deploy)>(
                new TupleKeyComparer()
            );

            foreach (Match m in linePattern.Matches(body))
            {
                var guid = m.Groups["guid"].Value.Trim();
                var solKey = m.Groups["sol"].Value.Trim(); // e.g., Debug|Any CPU
                var key = m.Groups["key"].Value.Trim();
                var val = m.Groups["val"].Value.Trim();    // e.g., Debug|Any CPU

                var k = (Guid: guid, SolKey: solKey);
                if (!state.TryGetValue(k, out var existing))
                {
                    existing = (ActiveCfg: null, Build: false, Deploy: false);
                }

                if (key.Equals("ActiveCfg", StringComparison.OrdinalIgnoreCase))
                    existing.ActiveCfg = val;
                else if (key.Equals("Build.0", StringComparison.OrdinalIgnoreCase))
                    existing.Build = true;
                else if (key.Equals("Deploy.0", StringComparison.OrdinalIgnoreCase))
                    existing.Deploy = true;

                state[k] = existing;
            }

            foreach (var kvp in state)
            {
                var guid = kvp.Key.Guid;
                var solKey = kvp.Key.SolKey;
                var (activeCfg, build, deploy) = kvp.Value;

                if (!projectsByGuid.TryGetValue(guid, out var project))
                    continue;

                var mapping = new SolutionConfigurationMapping
                {
                    Solution = ConfigurationPlatform.FromKey(solKey),
                    Project = ConfigurationPlatform.FromKey(activeCfg ?? solKey),
                    Build = build,
                    Deploy = deploy
                };

                project.ConfigurationMappings.Add(mapping);
            }
        }

        info.Projects = projectsByGuid.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return info;
    }

    private static bool IsSupportedProjectPath(string relativePath)
    {
        return relativePath.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) ||
               relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
               relativePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
               relativePath.EndsWith(".vcproj", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TupleKeyComparer : IEqualityComparer<(string Guid, string SolKey)>
    {
        public bool Equals((string Guid, string SolKey) x, (string Guid, string SolKey) y)
        {
            return string.Equals(x.Guid, y.Guid, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.SolKey, y.SolKey, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string Guid, string SolKey) obj)
        {
            return HashCode.Combine(
                obj.Guid.ToLowerInvariant(),
                obj.SolKey.ToLowerInvariant()
            );
        }
    }
}

