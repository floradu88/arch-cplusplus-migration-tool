namespace SolutionDependencyMapper.Cli;

public enum CliCommand
{
    AnalyzeSolution = 0,
    FindTools = 1
}

public sealed record CliOptions(
    CliCommand Command,
    string? SolutionPath,
    string? FindToolsRoot,
    bool AssumeVsEnv,
    bool AutoInstallPackages,
    bool PerConfigReferences,
    bool Parallel,
    int MaxParallelism,
    bool ResolveNuGet,
    bool CheckOutputs,
    bool ScanGac
)
{
    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        options = new CliOptions(
            Command: CliCommand.AnalyzeSolution,
            SolutionPath: null,
            FindToolsRoot: null,
            AssumeVsEnv: false,
            AutoInstallPackages: true,
            PerConfigReferences: false,
            Parallel: true,
            MaxParallelism: Environment.ProcessorCount,
            ResolveNuGet: false,
            CheckOutputs: false,
            ScanGac: false
        );
        error = null;

        if (args.Length == 0)
        {
            error = "No arguments provided.";
            return false;
        }

        // Commands
        if (args[0] is "--find-tools" or "--tools" or "-t")
        {
            options = options with
            {
                Command = CliCommand.FindTools,
                FindToolsRoot = args.Length > 1 ? args[1] : null
            };
            return true;
        }

        // Flags + positional args
        var argsList = args.ToList();

        if (argsList.Contains("--assume-vs-env") || argsList.Contains("--vs-env"))
        {
            argsList.Remove("--assume-vs-env");
            argsList.Remove("--vs-env");
            options = options with { AssumeVsEnv = true };
        }

        if (argsList.Contains("--no-auto-install-packages") || argsList.Contains("--no-auto-packages"))
        {
            argsList.Remove("--no-auto-install-packages");
            argsList.Remove("--no-auto-packages");
            options = options with { AutoInstallPackages = false };
        }

        if (argsList.Contains("--auto-install-packages") || argsList.Contains("--auto-packages"))
        {
            argsList.Remove("--auto-install-packages");
            argsList.Remove("--auto-packages");
            options = options with { AutoInstallPackages = true };
        }

        if (argsList.Contains("--per-config-refs") || argsList.Contains("--per-config-references"))
        {
            argsList.Remove("--per-config-refs");
            argsList.Remove("--per-config-references");
            options = options with { PerConfigReferences = true };
        }

        if (argsList.Contains("--no-per-config-refs") || argsList.Contains("--no-per-config-references"))
        {
            argsList.Remove("--no-per-config-refs");
            argsList.Remove("--no-per-config-references");
            options = options with { PerConfigReferences = false };
        }

        if (argsList.Contains("--resolve-nuget") || argsList.Contains("--resolve-nuget-packages"))
        {
            argsList.Remove("--resolve-nuget");
            argsList.Remove("--resolve-nuget-packages");
            options = options with { ResolveNuGet = true };
        }

        if (argsList.Contains("--no-resolve-nuget") || argsList.Contains("--no-resolve-nuget-packages"))
        {
            argsList.Remove("--no-resolve-nuget");
            argsList.Remove("--no-resolve-nuget-packages");
            options = options with { ResolveNuGet = false };
        }

        if (argsList.Contains("--check-outputs") || argsList.Contains("--validate-outputs"))
        {
            argsList.Remove("--check-outputs");
            argsList.Remove("--validate-outputs");
            options = options with { CheckOutputs = true };
        }

        if (argsList.Contains("--no-check-outputs") || argsList.Contains("--no-validate-outputs"))
        {
            argsList.Remove("--no-check-outputs");
            argsList.Remove("--no-validate-outputs");
            options = options with { CheckOutputs = false };
        }

        if (argsList.Contains("--scan-gac") || argsList.Contains("--scan-gac-msbuild"))
        {
            argsList.Remove("--scan-gac");
            argsList.Remove("--scan-gac-msbuild");
            options = options with { ScanGac = true };
        }

        if (argsList.Contains("--no-scan-gac"))
        {
            argsList.Remove("--no-scan-gac");
            options = options with { ScanGac = false };
        }

        if (argsList.Contains("--no-parallel"))
        {
            argsList.Remove("--no-parallel");
            options = options with { Parallel = false };
        }
        if (argsList.Contains("--parallel"))
        {
            argsList.Remove("--parallel");
            options = options with { Parallel = true };
        }

        // --max-parallelism N
        var mpi = argsList.FindIndex(a => a.Equals("--max-parallelism", StringComparison.OrdinalIgnoreCase));
        if (mpi >= 0)
        {
            if (mpi + 1 >= argsList.Count)
            {
                error = "Missing value for --max-parallelism";
                return false;
            }

            if (!int.TryParse(argsList[mpi + 1], out var maxPar) || maxPar < 1)
            {
                error = $"Invalid value for --max-parallelism: {argsList[mpi + 1]}";
                return false;
            }

            // remove flag + value
            argsList.RemoveAt(mpi + 1);
            argsList.RemoveAt(mpi);
            options = options with { MaxParallelism = maxPar };
        }

        // First remaining arg is the solution path (can be non-existent; validate later)
        var solutionPath = argsList.FirstOrDefault(arg => !arg.StartsWith("--"));
        options = options with { SolutionPath = solutionPath };
        return true;
    }
}


