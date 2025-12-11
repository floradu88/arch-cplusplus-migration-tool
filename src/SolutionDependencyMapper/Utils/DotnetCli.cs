using System.Diagnostics;

namespace SolutionDependencyMapper.Utils;

internal static class DotnetCli
{
    public static bool Restore(string projectPath)
    {
        if (!File.Exists(projectPath))
            return false;

        try
        {
            var projectDir = Path.GetDirectoryName(projectPath) ?? ".";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"restore \"{projectPath}\"",
                    WorkingDirectory = projectDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            _ = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"  ✓ Restored packages for: {Path.GetFileName(projectPath)}");
                return true;
            }

            Console.WriteLine($"  ⚠️  Warning: Package restore failed for {Path.GetFileName(projectPath)}");
            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"     Error: {error.Trim()}");
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Warning: Could not restore packages for {Path.GetFileName(projectPath)}: {ex.Message}");
            return false;
        }
    }
}


