namespace SolutionDependencyMapper.Utils;

/// <summary>
/// Context class that holds all discovered build tools for use throughout the application.
/// This is populated by ToolFinder at startup and used by other components.
/// </summary>
public class ToolsContext
{
    /// <summary>
    /// Dictionary of all discovered tools, keyed by tool name.
    /// </summary>
    public Dictionary<string, List<ToolFinder.FoundTool>> AllTools { get; set; } = new();

    /// <summary>
    /// Gets the best MSBuild path (prefers vswhere, then PATH, then common locations).
    /// </summary>
    public string? GetMSBuildPath()
    {
        if (!AllTools.TryGetValue("msbuild.exe", out var msbuilds) || msbuilds.Count == 0)
            return null;

        // Prefer vswhere result, then PATH, then common location
        var preferred = msbuilds
            .OrderBy(t => t.Source switch
            {
                ToolFinder.ToolSource.Vswhere => 0,
                ToolFinder.ToolSource.EnvironmentPath => 1,
                ToolFinder.ToolSource.CommonLocation => 2,
                _ => 3
            })
            .First();

        return preferred.Path;
    }

    /// <summary>
    /// Gets the best CMake path.
    /// </summary>
    public string? GetCmakePath()
    {
        if (!AllTools.TryGetValue("cmake.exe", out var cmakes) || cmakes.Count == 0)
            return null;

        // Prefer PATH, then common location
        var preferred = cmakes
            .OrderBy(t => t.Source switch
            {
                ToolFinder.ToolSource.EnvironmentPath => 0,
                ToolFinder.ToolSource.CommonLocation => 1,
                _ => 2
            })
            .First();

        return preferred.Path;
    }

    /// <summary>
    /// Gets the best path for a specific tool.
    /// </summary>
    public string? GetToolPath(string toolName)
    {
        if (!AllTools.TryGetValue(toolName, out var tools) || tools.Count == 0)
            return null;

        // Prefer vswhere, then PATH, then common location, then project root
        var preferred = tools
            .OrderBy(t => t.Source switch
            {
                ToolFinder.ToolSource.Vswhere => 0,
                ToolFinder.ToolSource.EnvironmentPath => 1,
                ToolFinder.ToolSource.CommonLocation => 2,
                ToolFinder.ToolSource.ProjectRoot => 3,
                _ => 4
            })
            .First();

        return preferred.Path;
    }

    /// <summary>
    /// Gets all paths for a specific tool.
    /// </summary>
    public List<string> GetToolPaths(string toolName)
    {
        if (!AllTools.TryGetValue(toolName, out var tools) || tools.Count == 0)
            return new List<string>();

        return tools.Select(t => t.Path).ToList();
    }

    /// <summary>
    /// Checks if a tool is available.
    /// </summary>
    public bool HasTool(string toolName)
    {
        return AllTools.ContainsKey(toolName) && AllTools[toolName].Count > 0;
    }

    /// <summary>
    /// Gets a formatted list of MSBuild paths for use in generated scripts.
    /// Returns paths in order of preference.
    /// </summary>
    public List<string> GetMSBuildPathsForScript()
    {
        if (!AllTools.TryGetValue("msbuild.exe", out var msbuilds) || msbuilds.Count == 0)
            return new List<string>();

        return msbuilds
            .OrderBy(t => t.Source switch
            {
                ToolFinder.ToolSource.Vswhere => 0,
                ToolFinder.ToolSource.EnvironmentPath => 1,
                ToolFinder.ToolSource.CommonLocation => 2,
                _ => 3
            })
            .Select(t => t.Path)
            .ToList();
    }
}

