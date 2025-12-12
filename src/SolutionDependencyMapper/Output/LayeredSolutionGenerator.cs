using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Output;

/// <summary>
/// Generates an alternate solution that groups projects into Solution Folders per build layer and
/// adds solution-level ProjectDependencies to enforce layer-by-layer builds in Visual Studio.
/// </summary>
public static class LayeredSolutionGenerator
{
    // VS Solution Folder project type GUID
    private const string SolutionFolderTypeGuid = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

    private static readonly Regex ProjectLineRegex = new(
        @"^\s*Project\(""\{(?<type>[A-F0-9\-]+)\}""\)\s*=\s*""(?<name>[^""]+)"",\s*""(?<path>[^""]+)"",\s*""\{(?<guid>[A-F0-9\-]+)\}""\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void Generate(string sourceSolutionPath, DependencyGraph graph, string outputSolutionPath)
    {
        var lines = File.ReadAllLines(sourceSolutionPath).ToList();

        // Build mapping from project GUID -> layer number
        var guidToLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in graph.BuildLayers)
        {
            foreach (var projectPath in layer.ProjectPaths)
            {
                if (!graph.Nodes.TryGetValue(projectPath, out var node))
                    continue;

                if (string.IsNullOrWhiteSpace(node.SolutionProjectGuid))
                    continue;

                var g = NormalizeGuid(node.SolutionProjectGuid);
                guidToLayer[g] = layer.LayerNumber;
            }
        }

        // Parse project blocks
        var blocks = ParseProjectBlocks(lines);

        // Create solution folders for layers
        var layerNumbers = guidToLayer.Values.Distinct().OrderBy(x => x).ToList();
        var layerFolderGuid = layerNumbers.ToDictionary(
            ln => ln,
            ln => NormalizeGuid(CreateStableGuid($"{Path.GetFullPath(sourceSolutionPath)}|layer|{ln}").ToString("B")));

        // Add/merge project dependencies based on previous layer
        var layerToGuids = layerNumbers.ToDictionary(ln => ln, ln => guidToLayer.Where(kv => kv.Value == ln).Select(kv => kv.Key).ToList());
        foreach (var block in blocks)
        {
            if (block.IsSolutionFolder)
                continue;

            if (!guidToLayer.TryGetValue(block.ProjectGuid, out var layer))
                continue;

            if (layer == layerNumbers.Min())
                continue;

            var prevLayer = layerNumbers.Where(x => x < layer).DefaultIfEmpty(layer).Max();
            if (prevLayer == layer)
                continue;

            var deps = layerToGuids.TryGetValue(prevLayer, out var prevGuids) ? prevGuids : new List<string>();
            if (deps.Count == 0)
                continue;

            var updated = AddOrUpdateProjectDependencies(block.Lines, deps);
            ReplaceRange(lines, block.StartIndex, block.EndIndex, updated);

            // After replacement, indices are stale; re-parse blocks and continue.
            blocks = ParseProjectBlocks(lines);
        }

        // Insert Solution Folder project blocks for each layer before Global
        var globalIdx = lines.FindIndex(l => l.Trim().Equals("Global", StringComparison.OrdinalIgnoreCase));
        if (globalIdx < 0)
            globalIdx = lines.Count;

        var folderBlocks = new List<string>();
        foreach (var ln in layerNumbers)
        {
            var folderGuid = layerFolderGuid[ln];
            var label = $"Layer {ln:00}";
            folderBlocks.Add($@"Project(""{SolutionFolderTypeGuid}"") = ""{label}"", ""{label}"", ""{folderGuid}""");
            folderBlocks.Add("EndProject");
        }

        if (folderBlocks.Count > 0)
        {
            // Insert after last existing project block, but before Global
            lines.InsertRange(globalIdx, folderBlocks);
        }

        // Ensure NestedProjects exists and map project -> layer folder
        var nestedMap = guidToLayer.ToDictionary(kv => kv.Key, kv => layerFolderGuid[kv.Value], StringComparer.OrdinalIgnoreCase);
        lines = UpsertNestedProjectsSection(lines, nestedMap);

        Directory.CreateDirectory(Path.GetDirectoryName(outputSolutionPath) ?? ".");
        File.WriteAllLines(outputSolutionPath, lines);
    }

    private static List<ProjectBlock> ParseProjectBlocks(List<string> lines)
    {
        var blocks = new List<ProjectBlock>();
        for (var i = 0; i < lines.Count; i++)
        {
            var m = ProjectLineRegex.Match(lines[i]);
            if (!m.Success)
                continue;

            var typeGuid = NormalizeGuid(m.Groups["type"].Value);
            var projectGuid = NormalizeGuid(m.Groups["guid"].Value);

            var start = i;
            var end = i;
            for (var j = i + 1; j < lines.Count; j++)
            {
                if (lines[j].Trim().Equals("EndProject", StringComparison.OrdinalIgnoreCase))
                {
                    end = j;
                    break;
                }
            }

            var blockLines = lines.GetRange(start, end - start + 1);
            blocks.Add(new ProjectBlock(start, end, projectGuid, IsSolutionFolder: NormalizeGuid(SolutionFolderTypeGuid) == typeGuid, Lines: blockLines));
            i = end;
        }
        return blocks;
    }

    private static List<string> AddOrUpdateProjectDependencies(List<string> projectBlockLines, List<string> dependencyGuids)
    {
        var deps = dependencyGuids.Select(NormalizeGuid).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (deps.Count == 0)
            return projectBlockLines;

        var startIdx = projectBlockLines.FindIndex(l => l.Contains("ProjectSection(ProjectDependencies)", StringComparison.OrdinalIgnoreCase));
        if (startIdx >= 0)
        {
            var endIdx = projectBlockLines.FindIndex(startIdx + 1, l => l.Trim().Equals("EndProjectSection", StringComparison.OrdinalIgnoreCase));
            if (endIdx < 0) endIdx = startIdx;

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = startIdx; i <= endIdx; i++)
            {
                var line = projectBlockLines[i].Trim();
                if (line.StartsWith("{", StringComparison.Ordinal) && line.Contains("="))
                {
                    var left = line.Split('=')[0].Trim();
                    existing.Add(NormalizeGuid(left));
                }
            }

            var insertAt = endIdx; // before EndProjectSection
            foreach (var g in deps)
            {
                if (existing.Contains(g))
                    continue;
                projectBlockLines.Insert(insertAt, $"\t\t{g} = {g}");
                insertAt++;
            }

            return projectBlockLines;
        }

        // No existing section: insert before EndProject
        var endProjectIdx = projectBlockLines.FindIndex(l => l.Trim().Equals("EndProject", StringComparison.OrdinalIgnoreCase));
        if (endProjectIdx < 0)
            return projectBlockLines;

        var section = new List<string>
        {
            "\tProjectSection(ProjectDependencies) = postProject"
        };
        section.AddRange(deps.Select(g => $"\t\t{g} = {g}"));
        section.Add("\tEndProjectSection");

        projectBlockLines.InsertRange(endProjectIdx, section);
        return projectBlockLines;
    }

    private static List<string> UpsertNestedProjectsSection(List<string> lines, Dictionary<string, string> projectToFolder)
    {
        var globalStart = lines.FindIndex(l => l.Trim().Equals("Global", StringComparison.OrdinalIgnoreCase));
        var globalEnd = lines.FindIndex(l => l.Trim().Equals("EndGlobal", StringComparison.OrdinalIgnoreCase));
        if (globalStart < 0 || globalEnd < 0 || globalEnd <= globalStart)
            return lines;

        var nestedStart = lines.FindIndex(globalStart, l => l.Contains("GlobalSection(NestedProjects)", StringComparison.OrdinalIgnoreCase));
        if (nestedStart >= 0)
        {
            var nestedEnd = lines.FindIndex(nestedStart + 1, l => l.Trim().Equals("EndGlobalSection", StringComparison.OrdinalIgnoreCase));
            if (nestedEnd < 0) nestedEnd = nestedStart;

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = nestedStart + 1; i < nestedEnd; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("{", StringComparison.Ordinal) && line.Contains("="))
                {
                    var left = line.Split('=')[0].Trim();
                    existing.Add(NormalizeGuid(left));
                }
            }

            var insertAt = nestedEnd;
            foreach (var kv in projectToFolder.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (existing.Contains(kv.Key))
                    continue;
                lines.Insert(insertAt, $"\t\t{kv.Key} = {kv.Value}");
                insertAt++;
            }

            return lines;
        }

        // Insert new section near end of Global (before EndGlobal)
        var sectionLines = new List<string>
        {
            "\tGlobalSection(NestedProjects) = preSolution"
        };
        sectionLines.AddRange(projectToFolder
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"\t\t{kv.Key} = {kv.Value}"));
        sectionLines.Add("\tEndGlobalSection");

        lines.InsertRange(globalEnd, sectionLines);
        return lines;
    }

    private static void ReplaceRange(List<string> lines, int start, int end, List<string> replacement)
    {
        lines.RemoveRange(start, end - start + 1);
        lines.InsertRange(start, replacement);
    }

    private static string NormalizeGuid(string guid)
    {
        var g = guid.Trim();
        if (!g.StartsWith("{", StringComparison.Ordinal)) g = "{" + g;
        if (!g.EndsWith("}", StringComparison.Ordinal)) g = g + "}";
        return g.ToUpperInvariant();
    }

    private static Guid CreateStableGuid(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes);
    }

    private sealed record ProjectBlock(int StartIndex, int EndIndex, string ProjectGuid, bool IsSolutionFolder, List<string> Lines);
}


