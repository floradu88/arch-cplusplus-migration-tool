using Microsoft.Build.Evaluation;
using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Core;

internal static class ReferenceValidator
{
    private static readonly HashSet<string> KnownSystemLibs = new(StringComparer.OrdinalIgnoreCase)
    {
        "kernel32.lib","user32.lib","gdi32.lib","advapi32.lib","ole32.lib","shell32.lib","comdlg32.lib","uuid.lib","shlwapi.lib",
        "ws2_32.lib","bcrypt.lib","iphlpapi.lib","winmm.lib","dbghelp.lib","version.lib","psapi.lib","crypt32.lib","ntdll.lib"
    };

    private static readonly HashSet<string> KnownSystemDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "kernel32.dll","user32.dll","gdi32.dll","advapi32.dll","ole32.dll","shell32.dll","comdlg32.dll","shlwapi.dll",
        "ws2_32.dll","bcrypt.dll","iphlpapi.dll","winmm.dll","dbghelp.dll","version.dll","psapi.dll","crypt32.dll","ntdll.dll"
    };

    public static void Validate(Project project, string projectPath, ProjectNode node)
    {
        ValidateProjectReferences(node);
        ValidateHeaderFiles(node);
        ValidateForcedIncludeFiles(project, projectPath, node);
        ValidateProjectItemsExist(node, node.ResourceFiles, "ResourceFile", "Resource file item does not exist on disk");
        ValidateProjectItemsExist(node, node.SourceFiles, "SourceFile", "Source file item does not exist on disk");
        ValidateProjectItemsExist(node, node.MasmFiles, "MasmFile", "MASM file item does not exist on disk");
        ValidateProjectItemsExist(node, node.IdlFiles, "IdlFile", "IDL file item does not exist on disk");
        ValidateHintPaths(project, projectPath, node);
        ValidateDirectories(project, projectPath, node, node.IncludeDirectories, "IncludeDirectory");
        ValidateDirectories(project, projectPath, node, node.AdditionalUsingDirectories, "AdditionalUsingDirectory");
        ValidateDirectories(project, projectPath, node, node.NativeLibraryDirectories, "LibraryDirectory");
        ValidateNativeArtifacts(project, projectPath, node);
    }

    private static void ValidateProjectReferences(ProjectNode node)
    {
        foreach (var path in node.ProjectDependencies)
        {
            if (!File.Exists(path))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "ProjectReference",
                    Reference = path,
                    ResolvedPath = path,
                    Details = "Referenced project file does not exist"
                });
            }
        }
    }

    private static void ValidateHeaderFiles(ProjectNode node)
    {
        foreach (var header in node.HeaderFiles)
        {
            if (!File.Exists(header))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "HeaderFile",
                    Reference = header,
                    ResolvedPath = header,
                    Details = "Header file item does not exist on disk"
                });
            }
        }
    }

    private static void ValidateProjectItemsExist(ProjectNode node, IEnumerable<string> items, string category, string details)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;

            // If still has macros, skip validation to avoid noise.
            if (item.Contains("$(") || item.Contains("%("))
                continue;

            if (!File.Exists(item))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = category,
                    Reference = item,
                    ResolvedPath = item,
                    Details = details
                });
            }
        }
    }

    private static void ValidateForcedIncludeFiles(Project project, string projectPath, ProjectNode node)
    {
        if (node.ForcedIncludeFiles.Count == 0)
            return;

        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;

        // Build a search path list similar to include resolution
        var includeDirs = new List<string> { projectDir };
        foreach (var d in node.IncludeDirectories)
        {
            var expanded = project.ExpandString(d).Trim();
            if (expanded.Contains("$(") || expanded.Contains("%("))
                continue;

            var resolved = Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(projectDir, expanded));

            includeDirs.Add(resolved);
        }

        foreach (var fi in node.ForcedIncludeFiles)
        {
            var raw = fi?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (raw.Contains("$(") || raw.Contains("%("))
                continue;

            // If already looks like a rooted/full path, validate directly.
            if (Path.IsPathRooted(raw) || raw.Contains('\\') || raw.Contains('/'))
            {
                var resolved = Path.IsPathRooted(raw) ? raw : Path.GetFullPath(Path.Combine(projectDir, raw));
                if (!File.Exists(resolved))
                {
                    node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                    {
                        Category = "ForcedIncludeFile",
                        Reference = fi,
                        ResolvedPath = resolved,
                        Details = "Forced include file does not exist"
                    });
                }
                continue;
            }

            // Otherwise treat as a filename and search in include dirs
            var found = includeDirs.Any(dir => File.Exists(Path.Combine(dir, raw)));
            if (!found)
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "ForcedIncludeFile",
                    Reference = fi,
                    Details = "Forced include file not found in project/include directories"
                });
            }
        }
    }

    private static void ValidateHintPaths(Project project, string projectPath, ProjectNode node)
    {
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        foreach (var item in project.GetItems("Reference"))
        {
            var hintPath = item.GetMetadataValue("HintPath");
            if (string.IsNullOrWhiteSpace(hintPath))
                continue;

            var expanded = project.ExpandString(hintPath).Trim();
            var resolved = Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(projectDir, expanded));

            if (!File.Exists(resolved))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "HintPath",
                    Reference = hintPath,
                    ResolvedPath = resolved,
                    Details = $"Assembly HintPath missing for Reference '{item.EvaluatedInclude}'"
                });
            }
        }
    }

    private static void ValidateDirectories(Project project, string projectPath, ProjectNode node, IEnumerable<string> dirs, string category)
    {
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;

        foreach (var d in dirs)
        {
            var raw = d?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            // Expand known MSBuild properties; if still contains macro tokens, skip validation to avoid noise.
            var expanded = project.ExpandString(raw).Trim();
            if (expanded.Contains("$(") || expanded.Contains("%("))
                continue;

            var resolved = Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(projectDir, expanded));

            if (!Directory.Exists(resolved))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = category,
                    Reference = raw,
                    ResolvedPath = resolved,
                    Details = "Directory does not exist"
                });
            }
        }
    }

    private static void ValidateNativeArtifacts(Project project, string projectPath, ProjectNode node)
    {
        // Build a search path list for non-system libs/dlls
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var libDirs = new List<string> { projectDir };

        foreach (var d in node.NativeLibraryDirectories)
        {
            var expanded = project.ExpandString(d).Trim();
            if (expanded.Contains("$(") || expanded.Contains("%("))
                continue;

            var resolved = Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(projectDir, expanded));

            libDirs.Add(resolved);
        }

        // Also consider environment LIB for MSVC
        var envLib = Environment.GetEnvironmentVariable("LIB");
        if (!string.IsNullOrWhiteSpace(envLib))
        {
            libDirs.AddRange(envLib.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        // Native libs: only validate non-system libs to avoid noise (Windows SDK libs are not easily discoverable cross-machine).
        foreach (var lib in node.NativeLibraries)
        {
            var name = lib.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name.Contains("$(") || name.Contains("%(")) continue;
            if (KnownSystemLibs.Contains(name)) continue;

            // If it's already a path, validate directly.
            if (name.Contains('\\') || name.Contains('/') || Path.IsPathRooted(name))
            {
                var resolved = Path.IsPathRooted(name) ? name : Path.GetFullPath(Path.Combine(projectDir, name));
                if (!File.Exists(resolved))
                {
                    node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                    {
                        Category = "NativeLibrary",
                        Reference = lib,
                        ResolvedPath = resolved,
                        Details = "Library file does not exist"
                    });
                }
                continue;
            }

            // Search in lib dirs
            var found = libDirs.Any(dir => File.Exists(Path.Combine(dir, name)));
            if (!found)
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "NativeLibrary",
                    Reference = lib,
                    Details = "Library not found in project/library directories or LIB environment"
                });
            }
        }

        // Delay-load DLLs: validate non-system only, and only when it's not a bare system dll name.
        foreach (var dll in node.NativeDelayLoadDlls)
        {
            var name = dll.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name.Contains("$(") || name.Contains("%(")) continue;
            if (KnownSystemDlls.Contains(name)) continue;

            if (name.Contains('\\') || name.Contains('/') || Path.IsPathRooted(name))
            {
                var resolved = Path.IsPathRooted(name) ? name : Path.GetFullPath(Path.Combine(projectDir, name));
                if (!File.Exists(resolved))
                {
                    node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                    {
                        Category = "DelayLoadDll",
                        Reference = dll,
                        ResolvedPath = resolved,
                        Details = "Delay-load DLL file does not exist"
                    });
                }
            }
        }
    }
}


