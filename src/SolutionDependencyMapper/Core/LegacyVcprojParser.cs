using System.Xml.Linq;
using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Core;

internal static class LegacyVcprojParser
{
    public static ProjectNode? TryParse(string projectPath, bool perConfigReferences, bool checkOutputs)
    {
        try
        {
            var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
            var doc = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root == null)
                return null;

            // Legacy vcproj typically has <VisualStudioProject ...>
            var name = root.Attribute("Name")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileNameWithoutExtension(projectPath);

            var version = root.Attribute("Version")?.Value?.Trim();
            var toolsVersion = !string.IsNullOrWhiteSpace(version) ? $"Legacy VCProj {version}" : "Legacy VCProj";

            var node = new ProjectNode
            {
                Name = name,
                Path = projectPath,
                ProjectType = "C++ Project (Legacy)",
                ToolsVersion = toolsVersion
            };

            // Platforms
            var platforms = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Platforms");
            if (platforms != null)
            {
                foreach (var p in platforms.Elements().Where(e => e.Name.LocalName == "Platform"))
                {
                    var platName = p.Attribute("Name")?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(platName))
                        node.Platforms.Add(platName);
                }
            }

            // Configurations
            var configsEl = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Configurations");
            var configs = new List<VcprojConfiguration>();
            if (configsEl != null)
            {
                foreach (var c in configsEl.Elements().Where(e => e.Name.LocalName == "Configuration"))
                {
                    var cfgName = c.Attribute("Name")?.Value?.Trim(); // Debug|Win32
                    if (string.IsNullOrWhiteSpace(cfgName))
                        continue;

                    var parts = cfgName.Split('|', 2, StringSplitOptions.TrimEntries);
                    var cfg = parts.Length > 0 ? parts[0] : cfgName;
                    var plat = parts.Length > 1 ? parts[1] : string.Empty;

                    if (!node.Configurations.Contains(cfg, StringComparer.OrdinalIgnoreCase))
                        node.Configurations.Add(cfg);
                    if (!string.IsNullOrWhiteSpace(plat) && !node.Platforms.Contains(plat, StringComparer.OrdinalIgnoreCase))
                        node.Platforms.Add(plat);

                    var key = string.IsNullOrWhiteSpace(plat) ? cfg : $"{cfg}|{plat}";
                    if (!node.ConfigurationPlatforms.Contains(key, StringComparer.OrdinalIgnoreCase))
                        node.ConfigurationPlatforms.Add(key);

                    configs.Add(new VcprojConfiguration(cfg, plat, c));
                }
            }

            // Files (headers/sources/resources/etc.)
            ExtractFiles(root, projectDir, node);

            // Aggregate refs from all configs
            foreach (var c in configs)
            {
                var compilerTool = FindTool(c.Element, "VCCLCompilerTool");
                var linkerTool = FindTool(c.Element, "VCLinkerTool");

                node.IncludeDirectories.AddRange(ParseList(ExpandSimpleMacros(compilerTool?.Attribute("AdditionalIncludeDirectories")?.Value, projectDir, projectPath, name)));
                node.ForcedIncludeFiles.AddRange(ParseFileListAsPaths(ExpandSimpleMacros(compilerTool?.Attribute("ForcedIncludeFiles")?.Value, projectDir, projectPath, name), projectDir));
                node.AdditionalUsingDirectories.AddRange(ParseList(ExpandSimpleMacros(compilerTool?.Attribute("AdditionalUsingDirectories")?.Value, projectDir, projectPath, name)));

                node.NativeLibraries.AddRange(ParseList(ExpandSimpleMacros(linkerTool?.Attribute("AdditionalDependencies")?.Value, projectDir, projectPath, name)));
                node.NativeDelayLoadDlls.AddRange(ParseList(ExpandSimpleMacros(linkerTool?.Attribute("DelayLoadDLLs")?.Value, projectDir, projectPath, name)));
                node.NativeLibraryDirectories.AddRange(ParseList(ExpandSimpleMacros(linkerTool?.Attribute("AdditionalLibraryDirectories")?.Value, projectDir, projectPath, name)));

                // Output file
                var outFile = ExpandSimpleMacros(linkerTool?.Attribute("OutputFile")?.Value, projectDir, projectPath, name);
                if (!string.IsNullOrWhiteSpace(outFile) && string.IsNullOrWhiteSpace(node.OutputBinary))
                {
                    node.OutputBinary = ResolvePath(projectDir, outFile);
                }
            }

            node.IncludeDirectories = node.IncludeDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.NativeLibraries = node.NativeLibraries.Where(s => !s.StartsWith("%(")).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.NativeDelayLoadDlls = node.NativeDelayLoadDlls.Where(s => !s.StartsWith("%(")).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.NativeLibraryDirectories = node.NativeLibraryDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.ForcedIncludeFiles = node.ForcedIncludeFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.AdditionalUsingDirectories = node.AdditionalUsingDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Derive output type/target
            node.OutputType = GuessOutputTypeFromOutputBinary(node.OutputBinary);
            node.TargetName = !string.IsNullOrWhiteSpace(node.OutputBinary) ? Path.GetFileNameWithoutExtension(node.OutputBinary) : node.Name;
            node.TargetExtension = !string.IsNullOrWhiteSpace(node.OutputBinary) ? Path.GetExtension(node.OutputBinary) : string.Empty;

            // Optional per-config snapshots (from config blocks, not MSBuild eval)
            if (perConfigReferences && configs.Count > 0)
            {
                foreach (var c in configs)
                {
                    var compilerTool = FindTool(c.Element, "VCCLCompilerTool");
                    var linkerTool = FindTool(c.Element, "VCLinkerTool");

                    var snap = new ProjectConfigurationSnapshot
                    {
                        Configuration = c.Configuration,
                        Platform = c.Platform
                    };

                    snap.IncludeDirectories = ParseList(ExpandSimpleMacros(compilerTool?.Attribute("AdditionalIncludeDirectories")?.Value, projectDir, projectPath, name));
                    snap.ForcedIncludeFiles = ParseFileListAsPaths(ExpandSimpleMacros(compilerTool?.Attribute("ForcedIncludeFiles")?.Value, projectDir, projectPath, name), projectDir);
                    snap.AdditionalUsingDirectories = ParseList(ExpandSimpleMacros(compilerTool?.Attribute("AdditionalUsingDirectories")?.Value, projectDir, projectPath, name));

                    snap.NativeLibraries = ParseList(ExpandSimpleMacros(linkerTool?.Attribute("AdditionalDependencies")?.Value, projectDir, projectPath, name))
                        .Where(s => !s.StartsWith("%(", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    snap.NativeDelayLoadDlls = ParseList(ExpandSimpleMacros(linkerTool?.Attribute("DelayLoadDLLs")?.Value, projectDir, projectPath, name))
                        .Where(s => !s.StartsWith("%(", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    snap.NativeLibraryDirectories = ParseList(ExpandSimpleMacros(linkerTool?.Attribute("AdditionalLibraryDirectories")?.Value, projectDir, projectPath, name));

                    // file items are global; copy them
                    snap.HeaderFiles = node.HeaderFiles;
                    snap.SourceFiles = node.SourceFiles;
                    snap.ResourceFiles = node.ResourceFiles;
                    snap.MasmFiles = node.MasmFiles;
                    snap.IdlFiles = node.IdlFiles;

                    if (checkOutputs)
                    {
                        var outFile = ExpandSimpleMacros(linkerTool?.Attribute("OutputFile")?.Value, projectDir, projectPath, name);
                        var resolved = !string.IsNullOrWhiteSpace(outFile) ? ResolvePath(projectDir, outFile) : null;
                        snap.OutputArtifact = new OutputArtifactStatus
                        {
                            Configuration = c.Configuration,
                            Platform = c.Platform,
                            ExpectedPath = resolved,
                            Exists = !string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved),
                            Details = string.IsNullOrWhiteSpace(resolved) ? "Could not determine OutputFile from legacy .vcproj" : null
                        };
                    }

                    node.ConfigurationSnapshots.Add(snap);
                }
            }

            // Best-effort validation without MSBuild evaluation
            BasicValidate(node);

            return node;
        }
        catch
        {
            return null;
        }
    }

    private static void ExtractFiles(XElement root, string projectDir, ProjectNode node)
    {
        var filesEl = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Files");
        if (filesEl == null)
            return;

        foreach (var fileEl in filesEl.Descendants().Where(e => e.Name.LocalName == "File"))
        {
            var rel = fileEl.Attribute("RelativePath")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(rel))
                continue;

            var full = ResolvePath(projectDir, rel);
            var ext = Path.GetExtension(full).ToLowerInvariant();
            switch (ext)
            {
                case ".h":
                case ".hpp":
                case ".inl":
                    node.HeaderFiles.Add(full);
                    break;
                case ".c":
                case ".cc":
                case ".cpp":
                case ".cxx":
                    node.SourceFiles.Add(full);
                    break;
                case ".rc":
                    node.ResourceFiles.Add(full);
                    break;
                case ".idl":
                    node.IdlFiles.Add(full);
                    break;
                case ".asm":
                case ".s":
                    node.MasmFiles.Add(full);
                    break;
            }
        }

        node.HeaderFiles = node.HeaderFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        node.SourceFiles = node.SourceFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        node.ResourceFiles = node.ResourceFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        node.IdlFiles = node.IdlFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        node.MasmFiles = node.MasmFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static XElement? FindTool(XElement configEl, string toolName)
    {
        // <Tool Name="VCCLCompilerTool" ... />
        return configEl.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Tool" &&
                                 string.Equals(e.Attribute("Name")?.Value, toolName, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExpandSimpleMacros(string? value, string projectDir, string projectPath, string projectName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Replace("$(ProjectDir)", EnsureTrailingSlash(projectDir), StringComparison.OrdinalIgnoreCase)
            .Replace("$(ProjectPath)", projectPath, StringComparison.OrdinalIgnoreCase)
            .Replace("$(ProjectName)", projectName, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSlash(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;
        return path.EndsWith("\\", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;
    }

    private static List<string> ParseList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Where(s => !s.StartsWith("%(", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseFileListAsPaths(string? raw, string projectDir)
    {
        var parts = ParseList(raw);
        return parts.Select(p => ResolvePath(projectDir, p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ResolvePath(string projectDir, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var p = path.Trim().Trim('"');
        if (Path.IsPathRooted(p))
            return p;

        return Path.GetFullPath(Path.Combine(projectDir, p));
    }

    private static string GuessOutputTypeFromOutputBinary(string? outputBinary)
    {
        if (string.IsNullOrWhiteSpace(outputBinary))
            return "Unknown";

        var ext = Path.GetExtension(outputBinary).ToLowerInvariant();
        return ext switch
        {
            ".exe" => "Exe",
            ".dll" => "DynamicLibrary",
            ".lib" => "StaticLibrary",
            _ => "Unknown"
        };
    }

    private static void BasicValidate(ProjectNode node)
    {
        // Validate item files we resolved to full paths
        foreach (var f in node.HeaderFiles)
        {
            if (!File.Exists(f))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "HeaderFile",
                    Reference = f,
                    ResolvedPath = f,
                    Details = "Header file item does not exist on disk"
                });
            }
        }

        foreach (var f in node.ForcedIncludeFiles)
        {
            if (!File.Exists(f))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "ForcedIncludeFile",
                    Reference = f,
                    ResolvedPath = f,
                    Details = "Forced include file does not exist"
                });
            }
        }

        foreach (var f in node.SourceFiles)
        {
            if (!File.Exists(f))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "SourceFile",
                    Reference = f,
                    ResolvedPath = f,
                    Details = "Source file item does not exist on disk"
                });
            }
        }

        foreach (var f in node.ResourceFiles)
        {
            if (!File.Exists(f))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "ResourceFile",
                    Reference = f,
                    ResolvedPath = f,
                    Details = "Resource file item does not exist on disk"
                });
            }
        }

        foreach (var f in node.MasmFiles)
        {
            if (!File.Exists(f))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "MasmFile",
                    Reference = f,
                    ResolvedPath = f,
                    Details = "MASM file item does not exist on disk"
                });
            }
        }

        foreach (var f in node.IdlFiles)
        {
            if (!File.Exists(f))
            {
                node.ReferenceValidationIssues.Add(new ReferenceValidationIssue
                {
                    Category = "IdlFile",
                    Reference = f,
                    ResolvedPath = f,
                    Details = "IDL file item does not exist on disk"
                });
            }
        }
    }

    private sealed record VcprojConfiguration(string Configuration, string Platform, XElement Element);
}


