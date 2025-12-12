using System.Diagnostics;

namespace SolutionDependencyMapper.Utils;

internal static class GacScanner
{
    public static List<string> TryListMicrosoftBuildAssembliesFromGac(ToolsContext? toolsContext)
    {
        if (!OperatingSystem.IsWindows())
            return new List<string>();

        var gacutilPath = toolsContext?.GetToolPath("gacutil.exe");

        // If ToolFinder didn't find it, try PATH resolution via ProcessStartInfo.
        var fileName = !string.IsNullOrWhiteSpace(gacutilPath) ? gacutilPath : "gacutil.exe";

        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = "/nologo /l",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            p.Start();
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            var combined = string.Join(Environment.NewLine, stdout, stderr);
            return ExtractMicrosoftBuildLines(combined);
        }
        catch
        {
            return new List<string>();
        }
    }

    public static List<string> ExtractMicrosoftBuildLines(string gacutilOutput)
    {
        if (string.IsNullOrWhiteSpace(gacutilOutput))
            return new List<string>();

        var lines = gacutilOutput
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Where(l => l.Contains("Microsoft.Build", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return lines;
    }
}


