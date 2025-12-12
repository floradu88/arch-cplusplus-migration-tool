using System.Text.Json;
using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Utils;

internal static class NuGetAssetsParser
{
    public static List<ResolvedNuGetPackage> TryParseResolvedPackages(string projectAssetsJsonPath)
    {
        if (!File.Exists(projectAssetsJsonPath))
            return new List<ResolvedNuGetPackage>();

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(projectAssetsJsonPath));
            var root = doc.RootElement;

            // Direct dependencies per TFM from project.frameworks.<tfm>.dependencies
            var directByTfm = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("project", out var projectEl) &&
                projectEl.TryGetProperty("frameworks", out var frameworksEl) &&
                frameworksEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var fw in frameworksEl.EnumerateObject())
                {
                    var tfm = fw.Name;
                    var direct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (fw.Value.TryGetProperty("dependencies", out var depsEl) && depsEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var d in depsEl.EnumerateObject())
                        {
                            direct.Add(d.Name);
                        }
                    }
                    directByTfm[tfm] = direct;
                }
            }

            // Resolved packages from targets.<tfm> object keys like "Newtonsoft.Json/13.0.3"
            var results = new List<ResolvedNuGetPackage>();
            if (root.TryGetProperty("targets", out var targetsEl) && targetsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var target in targetsEl.EnumerateObject())
                {
                    var tfm = SimplifyTargetFrameworkKey(target.Name);
                    if (target.Value.ValueKind != JsonValueKind.Object)
                        continue;

                    foreach (var entry in target.Value.EnumerateObject())
                    {
                        var key = entry.Name; // "Id/Version"
                        var slash = key.LastIndexOf('/');
                        if (slash <= 0 || slash >= key.Length - 1)
                            continue;

                        var id = key.Substring(0, slash);
                        var version = key.Substring(slash + 1);

                        // Only include packages (filter out project/assembly entries)
                        if (!IsPackageEntry(entry.Value))
                            continue;

                        var isDirect = directByTfm.TryGetValue(tfm, out var set) && set.Contains(id);

                        results.Add(new ResolvedNuGetPackage
                        {
                            Id = id,
                            Version = version,
                            TargetFramework = tfm,
                            IsDirect = isDirect
                        });
                    }
                }
            }

            return results
                .GroupBy(p => (p.TargetFramework ?? string.Empty, p.Id, p.Version), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(p => p.TargetFramework, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<ResolvedNuGetPackage>();
        }
    }

    private static bool IsPackageEntry(JsonElement entry)
    {
        // assets target entries can be of type "package", "project", etc.
        if (entry.ValueKind == JsonValueKind.Object &&
            entry.TryGetProperty("type", out var typeEl) &&
            typeEl.ValueKind == JsonValueKind.String)
        {
            return string.Equals(typeEl.GetString(), "package", StringComparison.OrdinalIgnoreCase);
        }

        // If 'type' isn't present, be conservative and treat it as a package.
        return true;
    }

    private static string SimplifyTargetFrameworkKey(string targetName)
    {
        // Normalize keys like ".NETCoreApp,Version=v8.0" -> "net8.0"
        // If already "net8.0", keep it.
        var lower = targetName.ToLowerInvariant();
        if (lower.StartsWith("net"))
            return targetName;

        // Very small heuristic for common names; keep original if unknown.
        if (lower.Contains("version=v") && lower.Contains("netcoreapp"))
        {
            var idx = lower.IndexOf("version=v", StringComparison.Ordinal);
            var ver = lower.Substring(idx + "version=v".Length);
            // ver like "8.0"
            return $"net{ver}";
        }

        if (lower.Contains("version=v") && lower.Contains("netframework"))
        {
            var idx = lower.IndexOf("version=v", StringComparison.Ordinal);
            var ver = lower.Substring(idx + "version=v".Length).Replace(".", string.Empty);
            return $"net{ver}";
        }

        return targetName;
    }
}


