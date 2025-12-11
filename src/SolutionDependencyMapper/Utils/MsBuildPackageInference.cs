namespace SolutionDependencyMapper.Utils;

internal static class MsBuildPackageInference
{
    public static IReadOnlyCollection<string> InferPackages(Exception ex)
    {
        if (ex == null) return Array.Empty<string>();

        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in EnumerateExceptionChain(ex))
        {
            var msg = e.Message ?? string.Empty;
            var lower = msg.ToLowerInvariant();

            if (!LooksLikeAssemblyOrTypeLoadFailure(lower))
                continue;

            if (!lower.Contains("microsoft.build"))
                continue;

            // Always include base Microsoft.Build when we detect a Microsoft.Build* load failure.
            packages.Add("Microsoft.Build");

            AddIfMentioned(packages, lower, "microsoft.build.framework", "Microsoft.Build.Framework");
            AddIfMentioned(packages, lower, "microsoft.build.utilities.core", "Microsoft.Build.Utilities.Core");
            AddIfMentioned(packages, lower, "microsoft.build.tasks.core", "Microsoft.Build.Tasks.Core");
            AddIfMentioned(packages, lower, "microsoft.build.engine", "Microsoft.Build.Engine");

            // Sometimes message contains type namespaces that imply dependencies.
            if (lower.Contains("microsoft.build.evaluation") || lower.Contains("microsoft.build.execution"))
            {
                packages.Add("Microsoft.Build.Framework");
            }
        }

        return packages.Count == 0 ? Array.Empty<string>() : packages.ToList();
    }

    public static bool IsMatch(Exception ex) => InferPackages(ex).Count > 0;

    private static bool LooksLikeAssemblyOrTypeLoadFailure(string lower)
    {
        // Intentionally conservative: avoid matching generic "could not find" noise.
        return
            lower.Contains("could not load file or assembly") ||
            lower.Contains("fileloadexception") ||
            lower.Contains("filenotfoundexception") ||
            lower.Contains("could not load type") ||
            lower.Contains("typeinitializationexception") ||
            lower.Contains("the type or namespace name") ||
            lower.Contains("could not resolve type") ||
            lower.Contains("could not resolve assembly") ||
            lower.Contains("could not load assembly");
    }

    private static IEnumerable<Exception> EnumerateExceptionChain(Exception ex)
    {
        for (var cur = ex; cur != null; cur = cur.InnerException!)
        {
            yield return cur;
            if (cur.InnerException == null) break;
        }
    }

    private static void AddIfMentioned(HashSet<string> packages, string lowerMessage, string needle, string packageName)
    {
        if (lowerMessage.Contains(needle))
        {
            packages.Add(packageName);
        }
    }
}


