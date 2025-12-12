using Microsoft.Build.Evaluation;

namespace SolutionDependencyMapper.Utils;

internal static class OutputArtifactResolver
{
    public static string? TryGetTargetPath(Project project, string projectPath)
    {
        // Prefer MSBuild-computed TargetPath if available
        var targetPath = project.GetPropertyValue("TargetPath");
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var expanded = project.ExpandString(targetPath).Trim();
            return ResolvePath(projectPath, expanded);
        }

        // Try TargetDir + TargetFileName
        var targetDir = project.GetPropertyValue("TargetDir");
        var targetFileName = project.GetPropertyValue("TargetFileName");
        if (!string.IsNullOrWhiteSpace(targetDir) && !string.IsNullOrWhiteSpace(targetFileName))
        {
            var expandedDir = project.ExpandString(targetDir).Trim();
            var expandedFile = project.ExpandString(targetFileName).Trim();
            var combined = Path.Combine(expandedDir, expandedFile);
            return ResolvePath(projectPath, combined);
        }

        // Try OutDir/OutputPath + TargetName/TargetExt (best-effort)
        var outDir = project.GetPropertyValue("OutDir");
        if (string.IsNullOrWhiteSpace(outDir))
            outDir = project.GetPropertyValue("OutputPath");
        var name = project.GetPropertyValue("TargetName");
        var ext = project.GetPropertyValue("TargetExt");

        if (!string.IsNullOrWhiteSpace(outDir) && !string.IsNullOrWhiteSpace(name))
        {
            var expandedDir = project.ExpandString(outDir).Trim();
            var expandedName = project.ExpandString(name).Trim();
            var expandedExt = project.ExpandString(ext ?? string.Empty).Trim();
            var combined = Path.Combine(expandedDir, expandedName + expandedExt);
            return ResolvePath(projectPath, combined);
        }

        return null;
    }

    private static string ResolvePath(string projectPath, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (Path.IsPathRooted(path))
            return path;

        var projectDir = Path.GetDirectoryName(projectPath) ?? ".";
        return Path.GetFullPath(Path.Combine(projectDir, path));
    }
}


