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
    bool AutoInstallPackages
)
{
    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        options = new CliOptions(
            Command: CliCommand.AnalyzeSolution,
            SolutionPath: null,
            FindToolsRoot: null,
            AssumeVsEnv: false,
            AutoInstallPackages: true
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

        // First remaining arg is the solution path (can be non-existent; validate later)
        var solutionPath = argsList.FirstOrDefault(arg => !arg.StartsWith("--"));
        options = options with { SolutionPath = solutionPath };
        return true;
    }
}


